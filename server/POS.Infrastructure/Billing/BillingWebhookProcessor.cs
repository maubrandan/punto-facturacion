using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.Billing;
using POS.Application.Common;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Billing;

/// <summary>
/// Procesa webhooks de Stripe/MP: verifica firma vía gateway y aplica eventos conocidos.
/// Payload real varía; soporta un envelope interno de desarrollo + campos comunes.
/// </summary>
public sealed class BillingWebhookProcessor : IBillingWebhookProcessor
{
    private readonly IBillingGatewayResolver _resolver;
    private readonly ApplicationDbContext _db;
    private readonly IPlatformAuditService _audit;
    private readonly ILogger<BillingWebhookProcessor> _logger;

    public BillingWebhookProcessor(
        IBillingGatewayResolver resolver,
        ApplicationDbContext db,
        IPlatformAuditService audit,
        ILogger<BillingWebhookProcessor> logger)
    {
        _resolver = resolver;
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Result<BillingWebhookResult>> ProcessAsync(
        BillingProvider provider,
        string rawBody,
        string? signatureHeader,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var gateway = _resolver.TryResolve(provider);
        if (gateway is null)
        {
            return Result<BillingWebhookResult>.Failure(
                "billing.webhook.provider_unknown",
                "Proveedor de webhook no soportado.");
        }

        var payload = new BillingWebhookPayload(provider, rawBody, signatureHeader, headers);
        if (!gateway.TryValidateWebhookSignature(payload, out var sigError))
        {
            return Result<BillingWebhookResult>.Failure(
                "billing.webhook.invalid_signature",
                sigError ?? "Firma de webhook inválida.");
        }

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            type ??= root.TryGetProperty("action", out var a) ? a.GetString() : null;
            type ??= root.TryGetProperty("event", out var e) ? e.GetString() : "unknown";

            var tenantId = root.TryGetProperty("tenantId", out var tid) ? tid.GetString() : null;
            if (string.IsNullOrWhiteSpace(tenantId)
                && root.TryGetProperty("data", out var data)
                && data.TryGetProperty("tenantId", out var nested))
            {
                tenantId = nested.GetString();
            }

            if (!string.IsNullOrWhiteSpace(tenantId)
                && type is not null
                && (type.Contains("paid", StringComparison.OrdinalIgnoreCase)
                    || type.Contains("invoice.paid", StringComparison.OrdinalIgnoreCase)
                    || type.Contains("payment.created", StringComparison.OrdinalIgnoreCase)))
            {
                var sub = await _db.TenantSubscriptions
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
                if (sub is not null && sub.Status == SubscriptionStatus.PastDue)
                {
                    var now = DateTime.UtcNow;
                    var start = sub.CurrentPeriodEndUtc < now ? now : sub.CurrentPeriodEndUtc;
                    var end = SubscriptionLifecycleRules.AddPeriod(start, sub.BillingCycle);
                    sub.ApplyRenewal(start, end, now);
                    await _db.SaveChangesAsync(cancellationToken);
                    await _audit.LogAsync(
                        new PlatformAuditEventData(
                            Action: "TenantSubscriptionRecoveredFromWebhook",
                            ResourceType: nameof(TenantSubscription),
                            ResourceId: tenantId,
                            Details: $"provider={provider}; type={type}",
                            AffectedTenantId: tenantId),
                        cancellationToken);
                }
            }

            var gatewayResult = await gateway.ProcessWebhookAsync(payload, cancellationToken);
            _logger.LogInformation(
                "Webhook {Provider} type={Type} tenant={TenantId} handled={Handled}",
                provider,
                type,
                tenantId,
                gatewayResult.Handled);

            return Result<BillingWebhookResult>.Ok(
                gatewayResult with { TenantId = tenantId ?? gatewayResult.TenantId, EventType = type });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Webhook body no es JSON válido; se acepta tras firma.");
            var gatewayResult = await gateway.ProcessWebhookAsync(payload, cancellationToken);
            return Result<BillingWebhookResult>.Ok(gatewayResult);
        }
    }
}
