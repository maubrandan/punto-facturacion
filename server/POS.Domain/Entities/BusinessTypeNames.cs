namespace POS.Domain.Entities;

/// <summary>Valores canónicos de rubro (tenant). Comparar con <see cref="StringComparer.OrdinalIgnoreCase"/>.</summary>
public static class BusinessTypeNames
{
    public const string Farmacia = "Farmacia";
    public const string Ferreteria = "Ferreteria";
    public const string Kiosco = "Kiosco";

    public static readonly IReadOnlyList<string> All =
    [
        Farmacia,
        Ferreteria,
        Kiosco
    ];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && All.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        foreach (var known in All)
        {
            if (string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        throw new ArgumentException($"Rubro desconocido: {value}", nameof(value));
    }
}
