using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Common;
using POS.Application.Contracts.Auth;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Domain.Tenant;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Email;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private const string ForgotPasswordAckMessage =
        "Si el email está registrado, te enviamos un enlace para restablecer la contraseña.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserTenantContext _tenantContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<AuthService> _logger;
    private readonly JwtOptions _jwt;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        ICurrentUserTenantContext tenantContext,
        IJwtTokenService jwtTokenService,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        IOptions<JwtOptions> jwt,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tenantContext = tenantContext;
        _jwtTokenService = jwtTokenService;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _logger = logger;
        _jwt = jwt.Value;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<AuthResponse>.Failure("auth.validation", "El email es obligatorio.");

        if (string.IsNullOrEmpty(request.Password))
            return Result<AuthResponse>.Failure("auth.validation", "La contraseña es obligatoria.");

        if (string.IsNullOrWhiteSpace(request.BusinessName))
            return Result<AuthResponse>.Failure("auth.validation", "El nombre del negocio es obligatorio.");

        if (!TryNormalizeBusinessType(request.BusinessType, out var businessType))
        {
            return Result<AuthResponse>.Failure(
                "auth.validation",
                "El rubro debe ser Farmacia, Ferreteria o Kiosco.");
        }

        var email = request.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
            return Result<AuthResponse>.Failure("auth.register.duplicate", "El email ya está registrado.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? email : request.FullName!.Trim(),
            BusinessType = businessType,
            AccountKind = UserAccountKind.TenantUser
        };
        var tenantId = Guid.NewGuid().ToString("N");
        user.TenantId = tenantId;
        _tenantContext.OverriddenTenantId = tenantId;

        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            var create = await _userManager.CreateAsync(user, request.Password);
            if (!create.Succeeded)
            {
                var details = string.Join(" ", create.Errors.Select(e => e.Description));
                return Result<AuthResponse>.Failure("auth.register.failed", details);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, TenantRoleNames.Admin);
            if (!roleResult.Succeeded)
            {
                var details = string.Join(" ", roleResult.Errors.Select(e => e.Description));
                return Result<AuthResponse>.Failure("auth.register.failed", details);
            }

            _context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = request.BusinessName.Trim(),
                BusinessType = businessType,
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var roles = new[] { TenantRoleNames.Admin };
            var accessToken = _jwtTokenService.CreateToken(user, businessType, roles, cancellationToken);
            return Result<AuthResponse>.Ok(MakeAuthResponse(user, accessToken, businessType, roles));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar el usuario.");
            return Result<AuthResponse>.Failure("auth.register.failed", "No se pudo completar el registro.");
        }
        finally
        {
            _tenantContext.OverriddenTenantId = null;
        }
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return Result<AuthResponse>.Failure("auth.login.invalid", "Email o contraseña inválidos.");

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null
            || !await _userManager.CheckPasswordAsync(user, request.Password)
            || !await _userManager.IsEmailConfirmedAsync(user))
        {
            return Result<AuthResponse>.Failure("auth.login.invalid", "Email o contraseña inválidos.");
        }

        if (user.LockoutEnabled
            && user.LockoutEnd is { } le
            && le > DateTimeOffset.UtcNow)
        {
            return Result<AuthResponse>.Failure("auth.login.locked", "La cuenta está bloqueada temporalmente.");
        }

        if (user.BlockedByPlatform)
        {
            return Result<AuthResponse>.Failure(
                "auth.login.platform_blocked",
                "La cuenta está bloqueada por soporte de plataforma.");
        }

        if (user.BlockedByTenant)
        {
            return Result<AuthResponse>.Failure(
                "auth.login.tenant_blocked",
                "La cuenta está bloqueada por el administrador del negocio.");
        }

        if (user.AccountKind == UserAccountKind.PlatformUser)
        {
            return Result<AuthResponse>.Failure(
                "auth.login.platform_user",
                "Las cuentas de consola de plataforma usan POST /api/platform/auth/login.");
        }

        string? tenantBusinessType = null;
        if (user.AccountKind == UserAccountKind.TenantUser
            && !string.IsNullOrWhiteSpace(user.TenantId))
        {
            var tenant = await _context.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);
            if (tenant is not null)
            {
                tenantBusinessType = string.IsNullOrWhiteSpace(tenant.BusinessType)
                    ? user.BusinessType
                    : tenant.BusinessType;

                if (tenant.Status == TenantStatus.Suspended)
                {
                    return Result<AuthResponse>.Failure(
                        "auth.login.tenant_suspended",
                        "El negocio está suspendido. Contacte a soporte.");
                }

                if (tenant.Status == TenantStatus.Closed)
                {
                    return Result<AuthResponse>.Failure(
                        "auth.login.tenant_closed",
                        "El negocio está cerrado.");
                }
            }
        }

        var businessType = tenantBusinessType ?? user.BusinessType;
        var roles = (await _userManager.GetRolesAsync(user))
            .Where(TenantRoleNames.IsKnownRole)
            .ToList();
        if (roles.Count == 0 && await _roleManager.RoleExistsAsync(TenantRoleNames.Admin))
        {
            await _userManager.AddToRoleAsync(user, TenantRoleNames.Admin);
            roles.Add(TenantRoleNames.Admin);
        }

        var accessToken = _jwtTokenService.CreateToken(user, businessType, roles, cancellationToken);
        return Result<AuthResponse>.Ok(MakeAuthResponse(user, accessToken, businessType, roles));
    }

    public async Task<Result<AuthMessageResponse>> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            return Result<AuthMessageResponse>.Failure(
                "auth.confirm.invalid",
                "El enlace de confirmación no es válido o expiró.");
        }

        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Result<AuthMessageResponse>.Failure(
                "auth.confirm.invalid",
                "El enlace de confirmación no es válido o expiró.");
        }

        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            return Result<AuthMessageResponse>.Ok(new AuthMessageResponse
            {
                Message = "El correo ya estaba confirmado. Ya podés iniciar sesión."
            });
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Token.Trim());
        if (!result.Succeeded)
        {
            var details = string.Join(" ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("ConfirmEmail falló para {Email}: {Details}", email, details);
            return Result<AuthMessageResponse>.Failure(
                "auth.confirm.invalid",
                "El enlace de confirmación no es válido o expiró.");
        }

        return Result<AuthMessageResponse>.Ok(new AuthMessageResponse
        {
            Message = "Correo confirmado correctamente. Ya podés iniciar sesión."
        });
    }

    public async Task<Result<AuthMessageResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrEmpty(request.NewPassword))
        {
            return Result<AuthMessageResponse>.Failure(
                "auth.reset.invalid",
                "No se pudo restablecer la contraseña. Revisá el enlace y la nueva contraseña.");
        }

        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Result<AuthMessageResponse>.Failure(
                "auth.reset.invalid",
                "No se pudo restablecer la contraseña. Revisá el enlace y la nueva contraseña.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token.Trim(), request.NewPassword);
        if (!result.Succeeded)
        {
            var details = string.Join(" ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("ResetPassword falló para {Email}: {Details}", email, details);

            if (result.Errors.Any(e =>
                    e.Code.Contains("Password", StringComparison.OrdinalIgnoreCase)))
            {
                return Result<AuthMessageResponse>.Failure(
                    "auth.reset.password_invalid",
                    details.Length > 0 ? details : "La nueva contraseña no cumple los requisitos.");
            }

            return Result<AuthMessageResponse>.Failure(
                "auth.reset.invalid",
                "No se pudo restablecer la contraseña. El enlace puede haber expirado.");
        }

        return Result<AuthMessageResponse>.Ok(new AuthMessageResponse
        {
            Message = "Contraseña actualizada. Ya podés iniciar sesión."
        });
    }

    public async Task<Result<AuthMessageResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<AuthMessageResponse>.Failure(
                "auth.forgot.validation",
                "El email es obligatorio.");
        }

        var email = request.Email.Trim();
        var ack = Result<AuthMessageResponse>.Ok(new AuthMessageResponse
        {
            Message = ForgotPasswordAckMessage
        });

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null
            || user.AccountKind != UserAccountKind.TenantUser
            || string.IsNullOrWhiteSpace(user.Email))
        {
            return ack;
        }

        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _emailSender.SendAsync(
                AuthEmailComposer.PasswordReset(_emailOptions, user.Email, token),
                cancellationToken);
            _logger.LogInformation("ForgotPassword: correo de reset enviado a {Email}", user.Email);
        }
        catch (Exception ex)
        {
            // Misma respuesta genérica: no filtrar existencia por fallos de envío.
            _logger.LogError(ex, "ForgotPassword: no se pudo enviar email de reset a {Email}", user.Email);
        }

        return ack;
    }

    public async Task<Result<AuthResponse>> PlatformLoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return Result<AuthResponse>.Failure("auth.platform.invalid", "Email o contraseña inválidos.");

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null
            || !await _userManager.CheckPasswordAsync(user, request.Password)
            || !await _userManager.IsEmailConfirmedAsync(user))
        {
            return Result<AuthResponse>.Failure("auth.platform.invalid", "Email o contraseña inválidos.");
        }

        if (user.LockoutEnabled
            && user.LockoutEnd is { } le
            && le > DateTimeOffset.UtcNow)
        {
            return Result<AuthResponse>.Failure("auth.platform.locked", "La cuenta está bloqueada temporalmente.");
        }

        if (user.AccountKind != UserAccountKind.PlatformUser)
        {
            return Result<AuthResponse>.Failure(
                "auth.platform.not_platform_user",
                "Esta cuenta no es de consola de plataforma. Use el login POS habitual.");
        }

        if (user.BlockedByPlatform)
        {
            return Result<AuthResponse>.Failure(
                "auth.platform.blocked",
                "La cuenta de plataforma está bloqueada.");
        }

        var assignedRoles = await _userManager.GetRolesAsync(user);
        var platformRoles = assignedRoles.Where(PlatformRoleNames.IsKnownRole).ToList();
        if (platformRoles.Count == 0)
        {
            return Result<AuthResponse>.Failure(
                "auth.platform.no_roles",
                "La cuenta no tiene ningún rol Platform.* asignado.");
        }

        var accessToken = _jwtTokenService.CreatePlatformToken(user, platformRoles, cancellationToken);
        return Result<AuthResponse>.Ok(MakePlatformAuthResponse(user, accessToken));
    }

    private AuthResponse MakeAuthResponse(
        ApplicationUser user,
        string accessToken,
        string businessType,
        IReadOnlyList<string> roles) =>
        new()
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = _jwt.ExpiresInMinutes * 60,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            TenantId = user.TenantId,
            BusinessType = businessType,
            Roles = roles.ToList()
        };

    private AuthResponse MakePlatformAuthResponse(ApplicationUser user, string accessToken) =>
        new()
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = _jwt.ExpiresInMinutes * 60,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            TenantId = string.Empty,
            BusinessType = PlatformScope.PlaceholderBusinessType,
            Roles = Array.Empty<string>()
        };

    private static bool TryNormalizeBusinessType(string? value, out string businessType)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? BusinessTypeNames.Kiosco : value.Trim();
        if (!BusinessTypeNames.IsKnown(candidate))
        {
            businessType = string.Empty;
            return false;
        }

        businessType = BusinessTypeNames.Normalize(candidate);
        return true;
    }
}
