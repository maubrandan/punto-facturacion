namespace POS.Domain.Billing.Afip;

/// <summary>Códigos de alícuota IVA del WSFEv1 (ARCA / ex AFIP).</summary>
public static class AfipVatAlicuotaMapper
{
    public static int ToAfipAlicuotaId(decimal taxRate)
    {
        var rate = Math.Round(taxRate, 2);
        return rate switch
        {
            0m => 3,
            2.5m => 9,
            5m => 8,
            10.5m => 4,
            21m => 5,
            27m => 6,
            _ => throw new ArgumentOutOfRangeException(
                nameof(taxRate),
                taxRate,
                "Alícuota IVA no soportada para WSFE.")
        };
    }
}
