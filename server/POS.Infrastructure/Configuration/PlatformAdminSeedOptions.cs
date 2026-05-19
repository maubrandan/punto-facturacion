namespace POS.Infrastructure.Configuration;

/// <summary>Opcional: un operador de plataforma en el arranque (solo dev/staging controlado).</summary>
public sealed class PlatformAdminSeedOptions
{
    public const string SectionName = "PlatformAdminSeed";

    public bool Enabled { get; init; }

    public string Email { get; init; } = "platform@local.dev";

    public string Password { get; init; } = "123456";

    public string FullName { get; init; } = "Operador plataforma (seed)";

    public string Role { get; init; } = "Platform.SuperAdmin";
}
