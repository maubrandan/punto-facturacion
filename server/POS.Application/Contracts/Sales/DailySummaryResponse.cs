namespace POS.Application.Contracts.Sales;

/// <summary>Resumen de facturación para un día (calendario UTC: 00:00 a 00:00 siguiente).</summary>
public sealed class DailySummaryResponse
{
    public decimal TotalFacturado { get; init; }

    public int VentasCount { get; init; }

    public Guid? TopProductId { get; init; }

    public string? TopProductName { get; init; }

    public int TopProductUnits { get; init; }
}
