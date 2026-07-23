using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS.Application.Billing;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Billing;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Platform;

namespace POS.Infrastructure.Billing;

public sealed class SelfServeUpgradeSubscriptionHandler : ISelfServeUpgradeSubscriptionHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBillingGatewayResolver _gateways;
    private readonly ISubscriptionInvoiceFactory _invoices;
    private readonly IPlatformAuditService _audit;
    private readonly IValidator<SelfServeUpgradeSubscriptionCommand> _validator;
    private readonly BillingOptions _billing;

    public SelfServeUpgradeSubscriptionHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IBillingGatewayResolver gateways,
        ISubscriptionInvoiceFactory invoices,
        IPlatformAuditService audit,
        IValidator<SelfServeUpgradeSubscriptionCommand> validator,
        IOptions<BillingOptions> billing)
    {
        _db = db;
        _currentUser = currentUser;
        _gateways = gateways;
        _invoices = invoices;
        _audit = audit;
        _validator = validator;
        _billing = billing.Value;
    }

    public async Task<Result<SelfServeUpgradeResultDto>> HandleAsync(
        SelfServeUpgradeSubscriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_gateways.IsNone || _billing.IsNone)
        {
            return Result<SelfServeUpgradeResultDto>.Failure(
                "subscription.provider_disabled",
                "Billing está en modo None: no se pueden mutar suscripciones.");
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SelfServeUpgradeResultDto>.Failure(
                "subscription.validation",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<SelfServeUpgradeResultDto>.Failure(
                "subscription.tenant_required",
                "Se requiere un tenant en el contexto.");
        }

        var sub = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (sub is null)
        {
            return Result<SelfServeUpgradeResultDto>.Failure(
                "subscription.not_found",
                "No existe suscripción para este tenant.");
        }

        if (!SubscriptionLifecycleRules.CanSelfServeMutate(sub.Status))
        {
            return Result<SelfServeUpgradeResultDto>.Failure(
                "subscription.lifecycle.invalid",
                "La suscripción no admite cambios self-serve en su estado actual.");
        }

        var plan = TenantPlanPresets.Normalize(command.PlanCode);
        if (!SubscriptionLifecycleRules.IsSelfServeUpgradeAllowed(sub.PlanCode, plan))
        {
            return Result<SelfServeUpgradeResultDto>.Failure(
                "subscription.upgrade.downgrade_forbidden",
                "Self-serve solo permite upgrade (o mismo plan con cambio de ciclo). Downgrades vía plataforma.");
        }

        var samePlan = string.Equals(sub.PlanCode, plan, StringComparison.OrdinalIgnoreCase);
        var sameCycle = sub.BillingCycle == command.BillingCycle;
        if (samePlan && sameCycle)
        {
            return Result<SelfServeUpgradeResultDto>.Failure(
                "subscription.upgrade.noop",
                "Ya tenés ese plan y ciclo de facturación.");
        }

        var gateway = _gateways.Resolve();
        var checkout = await gateway.CreateOrApplyPlanChangeAsync(
            new BillingCheckoutRequest(
                tenantId,
                plan,
                command.BillingCycle,
                command.SuccessUrl,
                command.CancelUrl),
            cancellationToken);

        if (!checkout.IsSuccess || checkout.Value is null)
        {
            return Result<SelfServeUpgradeResultDto>.Failure(
                checkout.ErrorCode ?? "billing.checkout_failed",
                checkout.Error ?? "No se pudo iniciar el cambio de plan.");
        }

        SubscriptionInvoiceDto? invoiceDto = null;
        var now = DateTime.UtcNow;
        var previousPlan = sub.PlanCode;

        if (checkout.Value.AppliedImmediately)
        {
            sub.PlanCode = plan;
            sub.BillingCycle = command.BillingCycle;
            sub.Provider = gateway.Provider;
            sub.CurrentPeriodStartUtc = now;
            sub.CurrentPeriodEndUtc = SubscriptionLifecycleRules.AddPeriod(now, command.BillingCycle);
            sub.ClearDunning(now);
            if (sub.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Trialing)
                sub.Status = SubscriptionStatus.Active;
            sub.CanceledAtUtc = null;
            sub.CancelAtPeriodEnd = false;
            sub.UpdatedAtUtc = now;

            var caps = TenantPlanPresets.Resolve(plan);
            var entitlement = await _db.TenantEntitlements
                .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);
            if (entitlement is null)
                _db.TenantEntitlements.Add(TenantEntitlementsMapper.ToNewRow(tenantId, caps, now));
            else
                TenantEntitlementsMapper.Apply(entitlement, caps, now);

            var invoice = await _invoices.CreateForPeriodAsync(
                sub,
                sub.CurrentPeriodStartUtc,
                sub.CurrentPeriodEndUtc,
                SubscriptionInvoiceStatus.Paid,
                now,
                $"Self-serve upgrade {previousPlan}→{plan}",
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(
                new PlatformAuditEventData(
                    Action: "TenantSubscriptionSelfServeUpgraded",
                    ResourceType: nameof(TenantSubscription),
                    ResourceId: tenantId,
                    Details:
                    $"plan={previousPlan}->{plan}; cycle={command.BillingCycle}; provider={gateway.Provider}; invoice={invoice.InvoiceNumber}",
                    AffectedTenantId: tenantId),
                cancellationToken);

            invoiceDto = SubscriptionInvoiceMapper.ToDto(invoice);
        }
        else
        {
            // Checkout externo: no mutar plan hasta webhook/pago. Guardamos intent en notas cortas.
            sub.Notes = Truncate(
                $"Pending checkout {gateway.Provider} session={checkout.Value.ExternalSessionId} target={plan}",
                500);
            sub.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var entitlementRow = await _db.TenantEntitlements
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);
        var matched = TenantEntitlementsMapper.FromRow(entitlementRow).MatchedPlanCode;

        return Result<SelfServeUpgradeResultDto>.Ok(
            new SelfServeUpgradeResultDto
            {
                Subscription = TenantSubscriptionMapper.ToDto(sub, matched),
                Invoice = invoiceDto,
                AppliedImmediately = checkout.Value.AppliedImmediately,
                CheckoutUrl = checkout.Value.CheckoutUrl,
                Message = checkout.Value.Message
            });
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

internal static class SubscriptionInvoiceMapper
{
    public static SubscriptionInvoiceDto ToDto(SubscriptionInvoice row) =>
        new()
        {
            Id = row.Id,
            TenantId = row.TenantId,
            InvoiceNumber = row.InvoiceNumber,
            Status = row.Status,
            PlanCode = row.PlanCode,
            BillingCycle = row.BillingCycle,
            PeriodStartUtc = row.PeriodStartUtc,
            PeriodEndUtc = row.PeriodEndUtc,
            Amount = row.Amount,
            Currency = row.Currency,
            Provider = row.Provider,
            ExternalInvoiceId = row.ExternalInvoiceId,
            DueAtUtc = row.DueAtUtc,
            PaidAtUtc = row.PaidAtUtc,
            Notes = row.Notes,
            CreatedAtUtc = row.CreatedAtUtc
        };
}
