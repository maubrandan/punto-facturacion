using System.Linq;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class PlatformTenantSubscriptionService : IPlatformTenantSubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly IPlatformDirectoryQuery _tenants;
    private readonly IPlatformAuditService _audit;
    private readonly IValidator<UpdateTenantSubscriptionCommand> _validator;
    private readonly BillingOptions _billing;

    public PlatformTenantSubscriptionService(
        ApplicationDbContext db,
        IPlatformDirectoryQuery tenants,
        IPlatformAuditService audit,
        IValidator<UpdateTenantSubscriptionCommand> validator,
        IOptions<BillingOptions> billing)
    {
        _db = db;
        _tenants = tenants;
        _audit = audit;
        _validator = validator;
        _billing = billing.Value;
    }

    public async Task<Result<TenantSubscriptionDto>> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var id = tenantId.Trim();
        if (await _tenants.GetTenantByIdAsync(id, cancellationToken) is null)
            return Result<TenantSubscriptionDto>.Failure("tenant.not_found", "No existe el tenant.");

        var row = await EnsureSubscriptionRowAsync(id, cancellationToken);
        return Result<TenantSubscriptionDto>.Ok(await ToDtoAsync(row, cancellationToken));
    }

    public async Task<Result<TenantSubscriptionDto>> UpdateAsync(
        UpdateTenantSubscriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_billing.IsNone)
        {
            return Result<TenantSubscriptionDto>.Failure(
                "subscription.provider_disabled",
                "Billing está en modo None: no se pueden mutar suscripciones desde consola.");
        }

        var v = await _validator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantSubscriptionDto>.Failure(
                "subscription.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var tenantId = command.TenantId.Trim();
        if (await _tenants.GetTenantByIdAsync(tenantId, cancellationToken) is null)
            return Result<TenantSubscriptionDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (!TenantPlanPresets.IsKnown(command.PlanCode))
        {
            return Result<TenantSubscriptionDto>.Failure(
                "subscription.plan_unknown",
                "planCode debe ser Starter, Pro o Unlimited.");
        }

        var now = DateTime.UtcNow;
        var row = await EnsureSubscriptionRowAsync(tenantId, cancellationToken);
        var previousPlan = row.PlanCode;
        var plan = TenantPlanPresets.Normalize(command.PlanCode);
        var planChanged = !string.Equals(previousPlan, plan, StringComparison.OrdinalIgnoreCase);

        if (planChanged && row.Status == SubscriptionStatus.Canceled && command.Status == SubscriptionStatus.Canceled)
        {
            return Result<TenantSubscriptionDto>.Failure(
                "subscription.lifecycle.invalid",
                "No se puede cambiar el plan de una suscripción cancelada sin reactivarla (status Active/Trialing).");
        }

        row.PlanCode = plan;
        row.Status = command.Status;
        row.BillingCycle = command.BillingCycle;
        row.Provider = BillingProvider.Manual;
        row.Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();
        row.CancelAtPeriodEnd = command.CancelAtPeriodEnd;
        row.TrialEndsAtUtc = command.TrialEndsAtUtc;
        row.UpdatedAtUtc = now;

        if (command.CurrentPeriodStartUtc.HasValue && command.CurrentPeriodEndUtc.HasValue)
        {
            row.CurrentPeriodStartUtc = NormalizeUtc(command.CurrentPeriodStartUtc.Value);
            row.CurrentPeriodEndUtc = NormalizeUtc(command.CurrentPeriodEndUtc.Value);
        }
        else if (planChanged)
        {
            row.CurrentPeriodStartUtc = now;
            row.CurrentPeriodEndUtc = TenantSubscriptionMapper.AddPeriod(now, command.BillingCycle);
        }

        if (command.Status == SubscriptionStatus.Canceled)
        {
            row.CanceledAtUtc ??= now;
        }
        else
        {
            row.CanceledAtUtc = null;
            if (!command.CancelAtPeriodEnd)
                row.CancelAtPeriodEnd = false;
        }

        if (planChanged)
        {
            var caps = TenantPlanPresets.Resolve(plan);
            var entitlement = await _db.TenantEntitlements
                .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);
            if (entitlement is null)
                _db.TenantEntitlements.Add(TenantEntitlementsMapper.ToNewRow(tenantId, caps, now));
            else
                TenantEntitlementsMapper.Apply(entitlement, caps, now);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                Action: planChanged ? "TenantSubscriptionPlanChanged" : "TenantSubscriptionUpdated",
                ResourceType: nameof(TenantSubscription),
                ResourceId: tenantId,
                Details:
                $"plan={previousPlan}->{plan}; status={command.Status}; cycle={command.BillingCycle}; entitlementsSynced={planChanged}; period={row.CurrentPeriodStartUtc:O}/{row.CurrentPeriodEndUtc:O}",
                Justification: command.Justification.Trim(),
                AffectedTenantId: tenantId),
            cancellationToken);

        return Result<TenantSubscriptionDto>.Ok(await ToDtoAsync(row, cancellationToken));
    }

    internal async Task<TenantSubscription> EnsureSubscriptionRowAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var existing = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var entitlements = await _db.TenantEntitlements
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);
        var plan = TenantSubscriptionMapper.InferPlanFromEntitlements(entitlements);
        var created = TenantSubscriptionMapper.CreateDefault(tenantId, plan, now);
        _db.TenantSubscriptions.Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task<TenantSubscriptionDto> ToDtoAsync(
        TenantSubscription row,
        CancellationToken cancellationToken)
    {
        var entitlement = await _db.TenantEntitlements
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == row.TenantId, cancellationToken);
        var matched = TenantEntitlementsMapper.FromRow(entitlement).MatchedPlanCode;
        return TenantSubscriptionMapper.ToDto(row, matched);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
