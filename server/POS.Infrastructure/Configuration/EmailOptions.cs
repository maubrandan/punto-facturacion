namespace POS.Infrastructure.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Logging | File | Smtp</summary>
    public string Provider { get; set; } = "Logging";

    public string FromAddress { get; set; } = "noreply@localhost";

    public string? FromDisplayName { get; set; } = "Punto Facturacion";

    /// <summary>Base URL del front para armar links (sin barra final).</summary>
    public string? PublicAppBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>Directorio de salida cuando Provider=File.</summary>
    public string? FileDirectory { get; set; }

    public SmtpEmailOptions Smtp { get; set; } = new();
}

public sealed class SmtpEmailOptions
{
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }
}
