using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>
/// Artículo de catálogo con precio neto y alícuota IVA para líneas de facturación electrónica (Argentina).
/// <see cref="ExtendedDataJson"/> puede incluir datos por rubro o códigos auxiliares (unidad de medida, GTIN, lote, etc.).
/// </summary>
public class Product : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string SKU { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public decimal NetPrice { get; set; }

    public decimal TaxRate { get; set; }

    public decimal FinalPrice { get => NetPrice * (1 + (TaxRate / 100m)); }

    public decimal Stock { get; set; }

    /// <summary>Último costo de compra unitario (neto) registrado; null si aún no hubo compras.</summary>
    public decimal? LastCost { get; set; }

    public string ExtendedDataJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
