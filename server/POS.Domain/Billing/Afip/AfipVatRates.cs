namespace POS.Domain.Billing.Afip;

/// <summary>
/// Alícuotas de IVA de referencia para comprobantes electrónicos en Argentina (ARCA / régimen ex AFIP).
/// Los valores deben alinearse con los códigos y porcentajes admitidos por el WS de facturación del contribuyente.
/// </summary>
public static class AfipVatRates
{
    public const decimal Iva21 = 21m;

    public const decimal Iva10_5 = 10.5m;

    public const decimal Iva27 = 27m;

    public const decimal Iva5 = 5m;

    public const decimal Iva2_5 = 2.5m;

    public const decimal Exento = 0m;
}
