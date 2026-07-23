using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Billing;

/// <summary>Procesa webhooks de pasarelas (firma + aplicación de eventos).</summary>
public interface IBillingWebhookProcessor
{
    Task<Result<BillingWebhookResult>> ProcessAsync(
        BillingProvider provider,
        string rawBody,
        string? signatureHeader,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
