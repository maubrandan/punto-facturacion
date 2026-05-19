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
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private static readonly HashSet<string> AllowedBusinessTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Farmacia",
        "Ferreteria",
        "Kiosco"
    };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserTenantContext _tenantContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly JwtOptions _jwt;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ICurrentUserTenantContext tenantContext,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwt,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _context = context;
        _tenantContext = tenantContext;
        _jwtTokenService = jwtTokenService;
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

            _context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = request.BusinessName.Trim(),
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var accessToken = _jwtTokenService.CreateToken(user, cancellationToken);
            return Result<AuthResponse>.Ok(MakeAuthResponse(user, accessToken));
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

        if (user.AccountKind == UserAccountKind.PlatformUser)
        {
            return Result<AuthResponse>.Failure(
                "auth.login.platform_user",
                "Las cuentas de consola de plataforma usan POST /api/platform/auth/login.");
        }

        if (user.AccountKind == UserAccountKind.TenantUser
            && !string.IsNullOrWhiteSpace(user.TenantId))
        {
            var tenant = await _context.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);
            if (tenant is not null)
            {
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

        var accessToken = _jwtTokenService.CreateToken(user, cancellationToken);
        return Result<AuthResponse>.Ok(MakeAuthResponse(user, accessToken));
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

    private AuthResponse MakeAuthResponse(ApplicationUser user, string accessToken) =>
        new()
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = _jwt.ExpiresInMinutes * 60,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            TenantId = user.TenantId,
            BusinessType = user.BusinessType
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
            BusinessType = PlatformScope.PlaceholderBusinessType
        };

    private static bool TryNormalizeBusinessType(string? value, out string businessType)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "Kiosco" : value.Trim();
        if (!AllowedBusinessTypes.Contains(candidate))
        {
            businessType = string.Empty;
            return false;
        }

        if (candidate.Equals("Farmacia", StringComparison.OrdinalIgnoreCase))
        {
            businessType = "Farmacia";
            return true;
        }

        if (candidate.Equals("Ferreteria", StringComparison.OrdinalIgnoreCase))
        {
            businessType = "Ferreteria";
            return true;
        }

        businessType = "Kiosco";
        return true;
    }
}
