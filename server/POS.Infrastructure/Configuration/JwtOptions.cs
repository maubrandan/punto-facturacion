namespace POS.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "POS";

    public string Audience { get; set; } = "pos-clients";

    public string SigningKey { get; set; } = string.Empty;

    public int ExpiresInMinutes { get; set; } = 60;
}
