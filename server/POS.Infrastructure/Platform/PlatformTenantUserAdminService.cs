using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Email;

namespace POS.Infrastructure.Platform;

public sealed class PlatformTenantUserAdminService : IPlatformTenantUserAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPlatformDirectoryQuery _tenants;
    private readonly IPlatformAuditService _audit;
    private readonly IValidator<PlatformUserActionRequest> _justificationValidator;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<PlatformTenantUserAdminService> _logger;

    public PlatformTenantUserAdminService(
        UserManager<ApplicationUser> userManager,
        IPlatformDirectoryQuery tenants,
        IPlatformAuditService audit,
        IValidator<PlatformUserActionRequest> justificationValidator,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<PlatformTenantUserAdminService> logger)
    {
        _userManager = userManager;
        _tenants = tenants;
        _audit = audit;
        _justificationValidator = justificationValidator;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public Task<Result<TenantUserSummaryDto>> BlockAsync(
        string tenantId,
        string userId,
        string justification,
        CancellationToken cancellationToken = default) =>
        SetBlockedAsync(tenantId, userId, justification, blocked: true, cancellationToken);

    public Task<Result<TenantUserSummaryDto>> UnblockAsync(
        string tenantId,
        string userId,
        string justification,
        CancellationToken cancellationToken = default) =>
        SetBlockedAsync(tenantId, userId, justification, blocked: false, cancellationToken);

    private async Task<Result<TenantUserSummaryDto>> SetBlockedAsync(
        string tenantId,
        string userId,
        string justification,
        bool blocked,
        CancellationToken cancellationToken)
    {
        var v = await _justificationValidator.ValidateAsync(
            new PlatformUserActionRequest { Justification = justification },
            cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantUserSummaryDto>.Failure(
                "platform.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var j = justification.Trim();
        if (await _tenants.GetTenantByIdAsync(tenantId, cancellationToken) is null)
            return Result<TenantUserSummaryDto>.Failure("tenant.not_found", "No existe el tenant.");

        var user = await _userManager.Users.FirstOrDefaultAsync(
            u => u.Id == userId && u.TenantId == tenantId,
            cancellationToken);
        if (user is null)
            return Result<TenantUserSummaryDto>.Failure("platform.users.not_found", "Usuario no encontrado en el tenant.");

        if (user.AccountKind != UserAccountKind.TenantUser)
        {
            return Result<TenantUserSummaryDto>.Failure(
                "platform.users.invalid_kind",
                "Solo se pueden gestionar usuarios de negocio del tenant.");
        }

        user.BlockedByPlatform = blocked;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return Result<TenantUserSummaryDto>.Failure(
                "platform.users.update_failed",
                string.Join(" ", update.Errors.Select(e => e.Description)));
        }

        var action = blocked ? "TenantUserBlockedByPlatform" : "TenantUserUnblockedByPlatform";
        await _audit.LogAsync(
            new PlatformAuditEventData(
                action,
                nameof(ApplicationUser),
                user.Id,
                $"tenantId={tenantId}",
                j,
                tenantId),
            cancellationToken);

        return Result<TenantUserSummaryDto>.Ok(MapSummary(user));
    }

    public async Task<Result<PlatformMutationAckDto>> RequestPasswordResetAsync(
        string tenantId,
        string userId,
        string justification,
        CancellationToken cancellationToken = default)
    {
        var v = await _justificationValidator.ValidateAsync(
            new PlatformUserActionRequest { Justification = justification },
            cancellationToken);
        if (!v.IsValid)
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        if (await _tenants.GetTenantByIdAsync(tenantId, cancellationToken) is null)
            return Result<PlatformMutationAckDto>.Failure("tenant.not_found", "No existe el tenant.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || user.TenantId != tenantId)
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.not_found",
                "Usuario no encontrado en el tenant.");
        }

        if (user.AccountKind != UserAccountKind.TenantUser)
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.invalid_kind",
                "Solo se pueden gestionar usuarios de negocio del tenant.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.email_missing",
                "El usuario no tiene email para enviar el restablecimiento.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        try
        {
            await _emailSender.SendAsync(
                AuthEmailComposer.PasswordReset(_emailOptions, user.Email, token),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar email de reset a {Email}", user.Email);
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.email_send_failed",
                "No se pudo enviar el correo de restablecimiento.");
        }

        await _audit.LogAsync(
            new PlatformAuditEventData(
                "TenantUserPasswordResetRequested",
                nameof(ApplicationUser),
                user.Id,
                $"Correo de restablecimiento enviado a {user.Email}.",
                justification.Trim(),
                tenantId),
            cancellationToken);

        return Result<PlatformMutationAckDto>.Ok(
            new PlatformMutationAckDto
            {
                Message = "Solicitud registrada. Se envió el correo de restablecimiento."
            });
    }

    public async Task<Result<PlatformMutationAckDto>> ResendEmailConfirmationAsync(
        string tenantId,
        string userId,
        string justification,
        CancellationToken cancellationToken = default)
    {
        var v = await _justificationValidator.ValidateAsync(
            new PlatformUserActionRequest { Justification = justification },
            cancellationToken);
        if (!v.IsValid)
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        if (await _tenants.GetTenantByIdAsync(tenantId, cancellationToken) is null)
            return Result<PlatformMutationAckDto>.Failure("tenant.not_found", "No existe el tenant.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || user.TenantId != tenantId)
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.not_found",
                "Usuario no encontrado en el tenant.");
        }

        if (user.AccountKind != UserAccountKind.TenantUser)
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.invalid_kind",
                "Solo se pueden gestionar usuarios de negocio del tenant.");
        }

        if (user.EmailConfirmed)
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.email_already_confirmed",
                "El correo ya está confirmado.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.email_missing",
                "El usuario no tiene email para reenviar la confirmación.");
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        try
        {
            await _emailSender.SendAsync(
                AuthEmailComposer.EmailConfirmation(_emailOptions, user.Email, token),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar email de confirmación a {Email}", user.Email);
            return Result<PlatformMutationAckDto>.Failure(
                "platform.users.email_send_failed",
                "No se pudo enviar el correo de confirmación.");
        }

        await _audit.LogAsync(
            new PlatformAuditEventData(
                "TenantUserEmailConfirmationResent",
                nameof(ApplicationUser),
                user.Id,
                $"Correo de confirmación enviado a {user.Email}.",
                justification.Trim(),
                tenantId),
            cancellationToken);

        return Result<PlatformMutationAckDto>.Ok(
            new PlatformMutationAckDto
            {
                Message = "Solicitud registrada. Se envió el correo de confirmación."
            });
    }

    private static TenantUserSummaryDto MapSummary(ApplicationUser u) =>
        new()
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            FullName = u.FullName,
            EmailConfirmed = u.EmailConfirmed,
            LockoutEnabled = u.LockoutEnabled,
            LockoutEnd = u.LockoutEnd,
            BlockedByPlatform = u.BlockedByPlatform
        };
}
