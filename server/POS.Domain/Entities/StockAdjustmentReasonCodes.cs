namespace POS.Domain.Entities;

/// <summary>
/// Catálogo compartido de motivos tipados para ajustes de stock (todos los rubros).
/// Comparar con <see cref="StringComparer.OrdinalIgnoreCase"/>.
/// </summary>
public static class StockAdjustmentReasonCodes
{
    public const string CountCorrection = "CountCorrection";
    public const string Damage = "Damage";
    public const string Theft = "Theft";
    public const string ExpiredDisposal = "ExpiredDisposal";
    public const string SupplierReturn = "SupplierReturn";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All =
    [
        CountCorrection,
        Damage,
        Theft,
        ExpiredDisposal,
        SupplierReturn,
        Other
    ];

    private static readonly IReadOnlyDictionary<string, string> LabelsByCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CountCorrection] = "Corrección de conteo",
            [Damage] = "Merma / daño",
            [Theft] = "Robo / faltante",
            [ExpiredDisposal] = "Baja por vencimiento",
            [SupplierReturn] = "Devolución a proveedor",
            [Other] = "Otro"
        };

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

        throw new ArgumentException($"Motivo de ajuste desconocido: {value}", nameof(value));
    }

    public static string GetLabel(string code)
    {
        var normalized = Normalize(code);
        return LabelsByCode[normalized];
    }

    public static IReadOnlyList<(string Code, string Label)> Options()
    {
        return All.Select(code => (code, LabelsByCode[code])).ToList();
    }
}
