using System.Text;
using System.Text.Json;

namespace POS.Domain.Billing.Afip;

/// <summary>
/// Genera la URL del QR de comprobantes electrónicos (especificación AFIP/ARCA).
/// </summary>
public static class AfipQrCodeBuilder
{
    public static string? BuildUrl(
        string sellerTaxId,
        int pointOfSale,
        int afipDocumentTypeCode,
        long voucherNumber,
        decimal totalAmount,
        string cae,
        DateTime voucherDateUtc,
        string? buyerTaxId)
    {
        if (string.IsNullOrWhiteSpace(sellerTaxId) || string.IsNullOrWhiteSpace(cae))
            return null;

        if (!long.TryParse(NormalizeTaxId(sellerTaxId), out var sellerCuit))
            return null;

        var hasBuyer = !string.IsNullOrWhiteSpace(buyerTaxId)
            && long.TryParse(NormalizeTaxId(buyerTaxId), out _);

        var payload = new Dictionary<string, object>
        {
            ["ver"] = 1,
            ["fecha"] = voucherDateUtc.ToString("yyyy-MM-dd"),
            ["cuit"] = sellerCuit,
            ["ptoVta"] = pointOfSale,
            ["tipoCmp"] = afipDocumentTypeCode,
            ["nroCmp"] = voucherNumber,
            ["importe"] = Math.Round(totalAmount, 2),
            ["moneda"] = "PES",
            ["ctz"] = 1,
            ["tipoCodAut"] = "E",
            ["codAut"] = long.Parse(cae.Trim())
        };

        if (hasBuyer)
        {
            payload["tipoDocRec"] = 80;
            payload["nroDocRec"] = long.Parse(NormalizeTaxId(buyerTaxId!));
        }
        else
        {
            payload["tipoDocRec"] = 99;
            payload["nroDocRec"] = 0;
        }

        var json = JsonSerializer.Serialize(payload);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return $"https://www.afip.gob.ar/fe/qr/?p={base64}";
    }

    private static string NormalizeTaxId(string taxId) =>
        new(taxId.Where(char.IsDigit).ToArray());
}
