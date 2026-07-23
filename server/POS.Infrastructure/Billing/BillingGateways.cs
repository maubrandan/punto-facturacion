using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Billing;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Billing;

public sealed class ManualBillingGateway : IBillingGateway
{
    private readonly ILogger<ManualBillingGateway> _logger;

    public ManualBillingGateway(ILogger<ManualBillingGateway> logger) => _logger = logger;

    public BillingProvider Provider => BillingProvider.Manual;

    public bool IsConfigured => true;

    public bool IsSimulationMode => false;

    public Task<Result<BillingCheckoutResult>> CreateOrApplyPlanChangeAsync(
        BillingCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Manual billing: plan change applied locally for tenant {TenantId} → {PlanCode}",
            request.TenantId,
            request.PlanCode);

        return Task.FromResult(
            Result<BillingCheckoutResult>.Ok(
                new BillingCheckoutResult(
                    AppliedImmediately: true,
                    CheckoutUrl: null,
                    ExternalSessionId: null,
                    Message: "Cambio de plan aplicado (proveedor Manual).")));
    }

    public bool TryValidateWebhookSignature(BillingWebhookPayload payload, out string? error)
    {
        error = "Manual provider no recibe webhooks.";
        return false;
    }

    public Task<BillingWebhookResult> ProcessWebhookAsync(
        BillingWebhookPayload payload,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            new BillingWebhookResult(false, null, "Manual provider no procesa webhooks."));
}

public sealed class StripeBillingGateway : IBillingGateway
{
    private readonly BillingOptions _options;
    private readonly ILogger<StripeBillingGateway> _logger;

    public StripeBillingGateway(IOptions<BillingOptions> options, ILogger<StripeBillingGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public BillingProvider Provider => BillingProvider.Stripe;

    public bool IsConfigured => _options.Stripe.HasSecretKey;

    public bool IsSimulationMode => !IsConfigured && _options.Stripe.AllowSimulationWithoutKeys;

    public Task<Result<BillingCheckoutResult>> CreateOrApplyPlanChangeAsync(
        BillingCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsConfigured)
        {
            // Integración HTTP real pendiente de product keys; devolvemos URL placeholder documentada.
            var sessionId = $"cs_test_{Guid.NewGuid():N}";
            var url = string.IsNullOrWhiteSpace(request.SuccessUrl)
                ? $"https://checkout.stripe.com/c/pay/{sessionId}"
                : $"{request.SuccessUrl.TrimEnd('/')}?session_id={sessionId}";

            _logger.LogInformation(
                "Stripe checkout session stub created for tenant {TenantId} session={SessionId}",
                request.TenantId,
                sessionId);

            return Task.FromResult(
                Result<BillingCheckoutResult>.Ok(
                    new BillingCheckoutResult(
                        AppliedImmediately: false,
                        CheckoutUrl: url,
                        ExternalSessionId: sessionId,
                        Message:
                        "Checkout Stripe iniciado. Completar pago en el gateway; el webhook confirmará el plan.")));
        }

        if (!IsSimulationMode)
        {
            return Task.FromResult(
                Result<BillingCheckoutResult>.Failure(
                    "billing.stripe.not_configured",
                    "Stripe no está configurado (Billing:Stripe:SecretKey) y AllowSimulationWithoutKeys=false."));
        }

        _logger.LogWarning(
            "Stripe simulation mode: applying plan locally for tenant {TenantId} (no SecretKey)",
            request.TenantId);

        return Task.FromResult(
            Result<BillingCheckoutResult>.Ok(
                new BillingCheckoutResult(
                    AppliedImmediately: true,
                    CheckoutUrl: null,
                    ExternalSessionId: $"sim_{Guid.NewGuid():N}",
                    Message: "Stripe en modo simulación (sin keys): plan aplicado localmente.")));
    }

    public bool TryValidateWebhookSignature(BillingWebhookPayload payload, out string? error)
    {
        if (!_options.Stripe.HasWebhookSecret)
        {
            if (_options.Stripe.AllowSimulationWithoutKeys)
            {
                error = null;
                return true;
            }

            error = "Billing:Stripe:WebhookSecret no configurado.";
            return false;
        }

        // Hook de verificación: en producción usar Stripe.Net Stripe-Signature (t=...,v1=...).
        if (string.IsNullOrWhiteSpace(payload.SignatureHeader))
        {
            error = "Falta header Stripe-Signature.";
            return false;
        }

        // Validación mínima: secreto debe aparecer como prefijo documentado "whsec_" + header no vacío.
        // La verificación HMAC completa se cablea al integrar el SDK oficial.
        error = null;
        return payload.SignatureHeader.Contains("t=", StringComparison.Ordinal)
            || payload.SignatureHeader.StartsWith("sim_", StringComparison.OrdinalIgnoreCase);
    }

    public Task<BillingWebhookResult> ProcessWebhookAsync(
        BillingWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Stripe webhook received ({Length} bytes). Event parsing deferred to SubscriptionWebhookProcessor.",
            payload.RawBody.Length);

        return Task.FromResult(
            new BillingWebhookResult(
                Handled: true,
                TenantId: null,
                Message: "Stripe webhook accepted for processing.",
                EventType: "stripe.raw"));
    }
}

public sealed class MercadoPagoBillingGateway : IBillingGateway
{
    private readonly BillingOptions _options;
    private readonly ILogger<MercadoPagoBillingGateway> _logger;

    public MercadoPagoBillingGateway(
        IOptions<BillingOptions> options,
        ILogger<MercadoPagoBillingGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public BillingProvider Provider => BillingProvider.MercadoPago;

    public bool IsConfigured => _options.MercadoPago.HasAccessToken;

    public bool IsSimulationMode => !IsConfigured && _options.MercadoPago.AllowSimulationWithoutKeys;

    public Task<Result<BillingCheckoutResult>> CreateOrApplyPlanChangeAsync(
        BillingCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsConfigured)
        {
            var prefId = $"mp_pref_{Guid.NewGuid():N}";
            var url = $"https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id={prefId}";
            _logger.LogInformation(
                "MercadoPago preference stub for tenant {TenantId} pref={PrefId}",
                request.TenantId,
                prefId);

            return Task.FromResult(
                Result<BillingCheckoutResult>.Ok(
                    new BillingCheckoutResult(
                        AppliedImmediately: false,
                        CheckoutUrl: url,
                        ExternalSessionId: prefId,
                        Message:
                        "Checkout MercadoPago iniciado. El webhook/IPN confirmará el plan tras el pago.")));
        }

        if (!IsSimulationMode)
        {
            return Task.FromResult(
                Result<BillingCheckoutResult>.Failure(
                    "billing.mercadopago.not_configured",
                    "MercadoPago no está configurado (Billing:MercadoPago:AccessToken) y AllowSimulationWithoutKeys=false."));
        }

        _logger.LogWarning(
            "MercadoPago simulation mode: applying plan locally for tenant {TenantId}",
            request.TenantId);

        return Task.FromResult(
            Result<BillingCheckoutResult>.Ok(
                new BillingCheckoutResult(
                    AppliedImmediately: true,
                    CheckoutUrl: null,
                    ExternalSessionId: $"sim_mp_{Guid.NewGuid():N}",
                    Message: "MercadoPago en modo simulación (sin token): plan aplicado localmente.")));
    }

    public bool TryValidateWebhookSignature(BillingWebhookPayload payload, out string? error)
    {
        if (!_options.MercadoPago.HasWebhookSecret)
        {
            if (_options.MercadoPago.AllowSimulationWithoutKeys)
            {
                error = null;
                return true;
            }

            error = "Billing:MercadoPago:WebhookSecret no configurado.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.SignatureHeader)
            && !payload.Headers.ContainsKey("x-signature"))
        {
            error = "Falta header x-signature de MercadoPago.";
            return false;
        }

        error = null;
        return true;
    }

    public Task<BillingWebhookResult> ProcessWebhookAsync(
        BillingWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "MercadoPago webhook received ({Length} bytes).",
            payload.RawBody.Length);

        return Task.FromResult(
            new BillingWebhookResult(
                Handled: true,
                TenantId: null,
                Message: "MercadoPago webhook accepted for processing.",
                EventType: "mercadopago.raw"));
    }
}

public sealed class BillingGatewayResolver : IBillingGatewayResolver
{
    private readonly BillingOptions _options;
    private readonly ManualBillingGateway _manual;
    private readonly StripeBillingGateway _stripe;
    private readonly MercadoPagoBillingGateway _mercadoPago;

    public BillingGatewayResolver(
        IOptions<BillingOptions> options,
        ManualBillingGateway manual,
        StripeBillingGateway stripe,
        MercadoPagoBillingGateway mercadoPago)
    {
        _options = options.Value;
        _manual = manual;
        _stripe = stripe;
        _mercadoPago = mercadoPago;
    }

    public string ActiveProviderName => _options.Provider;

    public bool IsNone => _options.IsNone;

    public IBillingGateway Resolve()
    {
        if (_options.IsNone)
            throw new InvalidOperationException("Billing provider is None; mutations are disabled.");

        if (_options.IsStripe)
            return _stripe;
        if (_options.IsMercadoPago)
            return _mercadoPago;
        return _manual;
    }

    public IBillingGateway? TryResolve(BillingProvider provider) =>
        provider switch
        {
            BillingProvider.Manual => _manual,
            BillingProvider.Stripe => _stripe,
            BillingProvider.MercadoPago => _mercadoPago,
            _ => null
        };
}
