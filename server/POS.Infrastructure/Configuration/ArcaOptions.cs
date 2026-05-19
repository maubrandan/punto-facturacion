namespace POS.Infrastructure.Configuration;

public sealed class ArcaOptions
{
    public const string SectionName = "Arca";

    public bool SandboxAutoApprove { get; set; } = true;

    public string AuthorizationEndpoint { get; set; } = "https://arca.invalid/ws/authorize";

    public int RetryMaxAttempts { get; set; } = 5;

    public int RetryBaseDelaySeconds { get; set; } = 30;

    public int RetryMaxDelayMinutes { get; set; } = 20;
}
