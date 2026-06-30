namespace POS.Infrastructure.Configuration;

public sealed class ArcaOptions
{
    public const string SectionName = "Arca";

    public bool SandboxAutoApprove { get; set; } = true;

    /// <summary>Si es true y el perfil tiene certificado, usa WSAA+WSFE directo contra AFIP/ARCA.</summary>
    public bool EnableDirectAfip { get; set; }

    public string AuthorizationEndpoint { get; set; } = "https://arca.invalid/ws/authorize";

    public string WsaaUrl { get; set; } = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";

    public string WsfeUrl { get; set; } = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";

    public string AfipServiceName { get; set; } = "wsfe";

    /// <summary>Directorio base opcional para rutas relativas de certificados (file:...).</summary>
    public string? CertificateBasePath { get; set; }

    public int RetryMaxAttempts { get; set; } = 5;

    public int RetryBaseDelaySeconds { get; set; } = 30;

    public int RetryMaxDelayMinutes { get; set; } = 20;
}
