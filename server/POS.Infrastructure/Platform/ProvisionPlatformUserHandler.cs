using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using POS.Application.Common;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;

namespace POS.Infrastructure.Platform;

public sealed class ProvisionPlatformUserHandler : IProvisionPlatformUserHandler
{
    private readonly IValidator<ProvisionPlatformUserCommand> _validator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ICurrentUserTenantContext _tenantContext;
    private readonly IPlatformAuditService _platformAudit;
    private readonly ILogger<ProvisionPlatformUserHandler> _logger;

    public ProvisionPlatformUserHandler(
        IValidator<ProvisionPlatformUserCommand> validator,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ICurrentUserTenantContext tenantContext,
        IPlatformAuditService platformAudit,
        ILogger<ProvisionPlatformUserHandler> logger)
    {
        _validator = validator;
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantContext = tenantContext;
        _platformAudit = platformAudit;
        _logger = logger;
    }

    public async Task<Result<ProvisionPlatformUserResult>> HandleAsync(
        ProvisionPlatformUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _validator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<ProvisionPlatformUserResult>.Failure(
                "platform.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        if (!UserAccountRules.IsValidTenantPair(UserAccountKind.PlatformUser, PlatformScope.ReservedTenantId))
        {
            return Result<ProvisionPlatformUserResult>.Failure("platform.invariant", "Inconsistencia de reglas de cuenta de plataforma.");
        }

        var email = command.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return Result<ProvisionPlatformUserResult>.Failure("platform.provision.duplicate", "El email ya está registrado.");
        }

        if (!await _roleManager.RoleExistsAsync(command.PlatformRole.Trim()))
        {
            return Result<ProvisionPlatformUserResult>.Failure(
                "platform.provision.role_missing",
                "El rol de plataforma no existe. Ejecute el seeder o migración.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = command.FullName.Trim(),
            TenantId = PlatformScope.ReservedTenantId,
            AccountKind = UserAccountKind.PlatformUser,
            BusinessType = PlatformScope.PlaceholderBusinessType
        };

        var previous = _tenantContext.OverriddenTenantId;
        try
        {
            _tenantContext.OverriddenTenantId = PlatformScope.ReservedTenantId;

            var create = await _userManager.CreateAsync(user, command.Password);
            if (!create.Succeeded)
            {
                var details = string.Join(" ", create.Errors.Select(e => e.Description));
                return Result<ProvisionPlatformUserResult>.Failure("platform.provision.create_failed", details);
            }

            var reloaded = await _userManager.FindByEmailAsync(email);
            if (reloaded is null)
            {
                return Result<ProvisionPlatformUserResult>.Failure(
                    "platform.provision.reload_failed",
                    "No se pudo recargar el usuario creado.");
            }

            var ar = await _userManager.AddToRoleAsync(reloaded, command.PlatformRole.Trim());
            if (!ar.Succeeded)
            {
                _logger.LogError("AddToRole fallo; eliminando usuario huérfano: {Id}", reloaded.Id);
                await _userManager.DeleteAsync(reloaded);
                return Result<ProvisionPlatformUserResult>.Failure(
                    "platform.provision.role",
                    string.Join(" ", ar.Errors.Select(e => e.Description)));
            }

            await _platformAudit.LogAsync(
                new PlatformAuditEventData(
                    "PlatformUserProvisioned",
                    nameof(ApplicationUser),
                    reloaded.Id,
                    $"role={command.PlatformRole}"),
                cancellationToken);

            return Result<ProvisionPlatformUserResult>.Ok(
                new ProvisionPlatformUserResult(reloaded.Id, email, command.PlatformRole.Trim()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aprovisionando usuario de plataforma");
            return Result<ProvisionPlatformUserResult>.Failure(
                "platform.provision.exception",
                "Error interno al crear el operador de plataforma.");
        }
        finally
        {
            _tenantContext.OverriddenTenantId = previous;
        }
    }
}
