using POS.Domain.Entities;

namespace POS.Domain.Billing.Afip;

public static class AfipComprobanteTypes
{
    public static int ToAfipCode(FiscalDocumentType documentType) =>
        documentType switch
        {
            FiscalDocumentType.InvoiceA => 1,
            FiscalDocumentType.InvoiceB => 6,
            FiscalDocumentType.CreditNoteA => 3,
            FiscalDocumentType.CreditNoteB => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null)
        };

    public static string ToDisplayLabel(FiscalDocumentType documentType) =>
        documentType switch
        {
            FiscalDocumentType.InvoiceA => "Factura A",
            FiscalDocumentType.InvoiceB => "Factura B",
            FiscalDocumentType.CreditNoteA => "Nota de Crédito A",
            FiscalDocumentType.CreditNoteB => "Nota de Crédito B",
            _ => documentType.ToString()
        };
}
