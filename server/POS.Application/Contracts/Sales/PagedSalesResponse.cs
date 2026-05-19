namespace POS.Application.Contracts.Sales;

public sealed class SaleSummaryResponse
{
    public Guid Id { get; init; }

    public DateTime Fecha { get; init; }

    public decimal Total { get; init; }

    public string UsuarioNombre { get; init; } = string.Empty;

    public int CantidadItems { get; init; }
}

public sealed class PagedSalesResponse
{
    public IReadOnlyList<SaleSummaryResponse> Items { get; init; } = Array.Empty<SaleSummaryResponse>();

    public int TotalCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }
}
