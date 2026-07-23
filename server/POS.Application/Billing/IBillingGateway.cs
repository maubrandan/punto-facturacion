using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Billing;

public sealed record BillingCheckoutRequest(
    string TenantId,
    string PlanCode,
    BillingCycle BillingCycle,
    string? SuccessUrl,
    string? CancelUrl);

public sealed record BillingCheckoutResult(
    bool AppliedImmediately,
    string? CheckoutUrl,
    string? ExternalSessionId,
    string Message);

public sealed record BillingWebhookPayload(
    BillingProvider Provider,
    string RawBody,
    string? SignatureHeader,
    IReadOnlyDictionary<string, string> Headers);

public sealed record BillingWebhookResult(
    bool Handled,
    string? TenantId,
    string Message,
    string? EventType = null);

/// <summary>Strategy de pasarela de cobro SaaS (Manual / Stripe / MercadoPago).</summary>
public interface IBillingGateway
{
    BillingProvider Provider { get; }

    /// <summary>True si hay keys/config suficientes para llamar al API remoto.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Sin keys: modo simulación local (aplica cambios sin cobro real).
    /// Con keys: checkout/redirect o sync remoto.
    /// </summary>
    bool IsSimulationMode { get; }

    Task<Result<BillingCheckoutResult>> CreateOrApplyPlanChangeAsync(
        BillingCheckoutRequest request,
        CancellationToken cancellationToken = default);

    bool TryValidateWebhookSignature(BillingWebhookPayload payload, out string? error);

    Task<BillingWebhookResult> ProcessWebhookAsync(
        BillingWebhookPayload payload,
        CancellationToken cancellationToken = default);
}

/// <summary>Resuelve el gateway activo según <c>Billing:Provider</c>.</summary>
public interface IBillingGatewayResolver
{
    /// <summary>None | Manual | Stripe | MercadoPago (config).</summary>
    string ActiveProviderName { get; }

    bool IsNone { get; }

    IBillingGateway Resolve();

    IBillingGateway? TryResolve(BillingProvider provider);
}
