namespace POS.Infrastructure.Configuration;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public bool Enabled { get; init; } = true;

    public string Email { get; init; } = "admin@admin.com";

    public string Password { get; init; } = "123456";

    public string FullName { get; init; } = "Administrador Sistema";

    public string BusinessType { get; init; } = "Farmacia";

    public string BusinessName { get; init; } = "Administracion (seed)";
}
