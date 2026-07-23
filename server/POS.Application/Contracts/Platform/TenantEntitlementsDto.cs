namespace POS.Application.Contracts.Platform;

public sealed class TenantEntitlementsDto
{
    public int? MaxProducts { get; init; }

    public int? MaxTenantUsers { get; init; }

    public bool SalesEnabled { get; init; } = true;

    /// <summary>
    /// Etiqueta de preset si los caps coinciden exactamente con Starter/Pro/Unlimited; null si es custom.
    /// Solo informativo (no se persiste un plan).
    /// </summary>
    public string? MatchedPlanCode { get; init; }
}
