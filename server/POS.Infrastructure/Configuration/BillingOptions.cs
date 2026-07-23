namespace POS.Infrastructure.Configuration;

/// <summary>
/// Billing SaaS. <c>Provider</c>: None | Manual | Stripe | MercadoPago.
/// Keys vía user-secrets / env; nunca commitear secretos reales.
/// </summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>None | Manual | Stripe | MercadoPago.</summary>
    public string Provider { get; set; } = "Manual";

    /// <summary>Días de gracia en PastDue antes de cancelar.</summary>
    public int GracePeriodDays { get; set; } = 7;

    /// <summary>Intervalo mínimo entre intentos de dunning (horas).</summary>
    public int DunningIntervalHours { get; set; } = 24;

    public int MaxDunningAttempts { get; set; } = 3;

    /// <summary>Polling de jobs (segundos).</summary>
    public int JobsPollSeconds { get; set; } = 60;

    public bool EnableRenewalJob { get; set; } = true;

    public bool EnableDunningJob { get; set; } = true;

    public StripeBillingOptions Stripe { get; set; } = new();

    public MercadoPagoBillingOptions MercadoPago { get; set; } = new();

    public bool IsNone =>
        string.Equals(Provider, "None", StringComparison.OrdinalIgnoreCase);

    public bool IsManual =>
        string.Equals(Provider, "Manual", StringComparison.OrdinalIgnoreCase);

    public bool IsStripe =>
        string.Equals(Provider, "Stripe", StringComparison.OrdinalIgnoreCase);

    public bool IsMercadoPago =>
        string.Equals(Provider, "MercadoPago", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Provider, "MP", StringComparison.OrdinalIgnoreCase);
}

public sealed class StripeBillingOptions
{
    public string? SecretKey { get; set; }

    public string? PublishableKey { get; set; }

    public string? WebhookSecret { get; set; }

    /// <summary>Si true y faltan keys, el adapter simula checkout localmente.</summary>
    public bool AllowSimulationWithoutKeys { get; set; } = true;

    public bool HasSecretKey => !string.IsNullOrWhiteSpace(SecretKey);

    public bool HasWebhookSecret => !string.IsNullOrWhiteSpace(WebhookSecret);
}

public sealed class MercadoPagoBillingOptions
{
    public string? AccessToken { get; set; }

    public string? WebhookSecret { get; set; }

    public bool AllowSimulationWithoutKeys { get; set; } = true;

    public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);

    public bool HasWebhookSecret => !string.IsNullOrWhiteSpace(WebhookSecret);
}
