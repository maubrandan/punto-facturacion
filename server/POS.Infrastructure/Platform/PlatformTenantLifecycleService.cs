using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Domain.Tenant;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class PlatformTenantLifecycleService : IPlatformTenantLifecycleService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserTenantContext _tenantContext;
    private readonly IPlatformAuditService _audit;
    private readonly IValidator<CreatePlatformTenantCommand> _createValidator;
    private readonly IValidator<UpdatePlatformTenantCommand> _updateValidator;
    private readonly IValidator<SuspendPlatformTenantCommand> _suspendValidator;
    private readonly IValidator<UnsuspendPlatformTenantCommand> _unsuspendValidator;
    private readonly IValidator<ClosePlatformTenantCommand> _closeValidator;
    private readonly IValidator<ReopenPlatformTenantCommand> _reopenValidator;

    public PlatformTenantLifecycleService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ICurrentUserTenantContext tenantContext,
        IPlatformAuditService audit,
        IValidator<CreatePlatformTenantCommand> createValidator,
        IValidator<UpdatePlatformTenantCommand> updateValidator,
        IValidator<SuspendPlatformTenantCommand> suspendValidator,
        IValidator<UnsuspendPlatformTenantCommand> unsuspendValidator,
        IValidator<ClosePlatformTenantCommand> closeValidator,
        IValidator<ReopenPlatformTenantCommand> reopenValidator)
    {
        _db = db;
        _userManager = userManager;
        _tenantContext = tenantContext;
        _audit = audit;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _suspendValidator = suspendValidator;
        _unsuspendValidator = unsuspendValidator;
        _closeValidator = closeValidator;
        _reopenValidator = reopenValidator;
    }

    public async Task<Result<TenantDetailDto>> CreateAsync(
        CreatePlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var adminEmail = command.AdminEmail.Trim();
        if (await _userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.onboarding.duplicate_email",
                "El email del administrador ya está registrado.");
        }

        var tenantId = Guid.NewGuid().ToString("N");
        var businessType = BusinessTypeNames.Normalize(command.BusinessType);
        var now = DateTime.UtcNow;
        var previousOverride = _tenantContext.OverriddenTenantId;
        _tenantContext.OverriddenTenantId = tenantId;

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var entity = new Tenant
            {
                Id = tenantId,
                Name = command.Name.Trim(),
                ContactEmail = string.IsNullOrWhiteSpace(command.ContactEmail)
                    ? null
                    : command.ContactEmail.Trim(),
                BusinessType = businessType,
                Status = TenantStatus.Active,
                CreatedAt = now
            };

            _db.Tenants.Add(entity);

            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = string.IsNullOrWhiteSpace(command.AdminFullName)
                    ? adminEmail
                    : command.AdminFullName.Trim(),
                TenantId = tenantId,
                BusinessType = businessType,
                AccountKind = UserAccountKind.TenantUser
            };

            var createUser = await _userManager.CreateAsync(admin, command.AdminPassword);
            if (!createUser.Succeeded)
            {
                var details = string.Join(" ", createUser.Errors.Select(e => e.Description));
                return Result<TenantDetailDto>.Failure("tenant.onboarding.admin_create_failed", details);
            }

            var roleResult = await _userManager.AddToRoleAsync(admin, TenantRoleNames.Admin);
            if (!roleResult.Succeeded)
            {
                var details = string.Join(" ", roleResult.Errors.Select(e => e.Description));
                return Result<TenantDetailDto>.Failure("tenant.onboarding.role_failed", details);
            }

            var entitlements = TenantPlanPresets.ResolveForCreate(command);
            _db.TenantEntitlements.Add(TenantEntitlementsMapper.ToNewRow(tenantId, entitlements, now));

            var planLabel = string.IsNullOrWhiteSpace(command.PlanCode)
                ? TenantPlanPresets.Unlimited
                : TenantPlanPresets.Normalize(command.PlanCode);
            _db.TenantSubscriptions.Add(TenantSubscriptionMapper.CreateDefault(tenantId, planLabel, now));

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _audit.LogAsync(
                new PlatformAuditEventData(
                    Action: "TenantCreated",
                    ResourceType: "Tenant",
                    ResourceId: tenantId,
                    Details:
                    $"businessType={businessType}; adminUserId={admin.Id}; adminEmail={adminEmail}; plan={planLabel}; subscriptionPlan={planLabel}; maxProducts={entitlements.MaxProducts?.ToString() ?? "null"}; maxTenantUsers={entitlements.MaxTenantUsers?.ToString() ?? "null"}; salesEnabled={entitlements.SalesEnabled}",
                    AffectedTenantId: tenantId),
                cancellationToken);

            return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
        }
        catch
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.onboarding.failed",
                "No se pudo completar el alta del tenant.");
        }
        finally
        {
            _tenantContext.OverriddenTenantId = previousOverride;
        }
    }

    public async Task<Result<TenantDetailDto>> UpdateAsync(
        UpdatePlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken);
        if (entity is null)
            return Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (!TenantLifecycleRules.CanUpdate(entity.Status))
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.closed",
                "No se puede editar un negocio cerrado.");
        }

        entity.Name = command.Name.Trim();
        entity.ContactEmail = string.IsNullOrWhiteSpace(command.ContactEmail)
            ? null
            : command.ContactEmail.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                Action: "TenantUpdated",
                ResourceType: "Tenant",
                ResourceId: entity.Id,
                Details: $"name={entity.Name}; contactEmail={entity.ContactEmail ?? ""}",
                AffectedTenantId: entity.Id),
            cancellationToken);

        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }

    public async Task<Result<TenantDetailDto>> SuspendAsync(
        SuspendPlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _suspendValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken);
        if (entity is null)
            return Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (entity.Status == TenantStatus.Suspended)
            return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));

        if (entity.Status == TenantStatus.Closed)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.invalid",
                "No se puede suspender un negocio cerrado.");
        }

        if (!TenantLifecycleRules.CanSuspend(entity.Status))
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.invalid",
                "Transición de estado no válida.");
        }

        var now = DateTime.UtcNow;
        entity.Status = TenantStatus.Suspended;
        entity.SuspendedAt = now;
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                Action: "TenantSuspended",
                ResourceType: "Tenant",
                ResourceId: entity.Id,
                Details: "status=Suspended",
                AffectedTenantId: entity.Id),
            cancellationToken);

        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }

    public async Task<Result<TenantDetailDto>> UnsuspendAsync(
        UnsuspendPlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _unsuspendValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken);
        if (entity is null)
            return Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (entity.Status == TenantStatus.Active)
            return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));

        if (!TenantLifecycleRules.CanUnsuspend(entity.Status))
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.invalid",
                "Solo se puede reactivar un negocio suspendido.");
        }

        var now = DateTime.UtcNow;
        entity.Status = TenantStatus.Active;
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                Action: "TenantUnsuspended",
                ResourceType: "Tenant",
                ResourceId: entity.Id,
                Details: "status=Active",
                AffectedTenantId: entity.Id),
            cancellationToken);

        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }

    public async Task<Result<TenantDetailDto>> CloseAsync(
        ClosePlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _closeValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken);
        if (entity is null)
            return Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (entity.Status == TenantStatus.Closed)
            return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));

        if (!TenantLifecycleRules.CanClose(entity.Status))
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.invalid",
                "No se puede cerrar desde el estado actual.");
        }

        var now = DateTime.UtcNow;
        entity.Status = TenantStatus.Closed;
        entity.ClosedAt = now;
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                Action: "TenantClosed",
                ResourceType: "Tenant",
                ResourceId: entity.Id,
                Details: "status=Closed",
                AffectedTenantId: entity.Id),
            cancellationToken);

        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }

    public async Task<Result<TenantDetailDto>> ReopenAsync(
        ReopenPlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _reopenValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken);
        if (entity is null)
            return Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (entity.Status == TenantStatus.Active)
            return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));

        if (!TenantLifecycleRules.CanReopen(entity.Status))
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.invalid",
                "Solo se puede reabrir un negocio cerrado.");
        }

        var justification = command.Justification.Trim();
        var now = DateTime.UtcNow;
        entity.Status = TenantStatus.Active;
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                Action: "TenantReopened",
                ResourceType: "Tenant",
                ResourceId: entity.Id,
                Details: "status=Active",
                Justification: justification,
                AffectedTenantId: entity.Id),
            cancellationToken);

        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }
}
