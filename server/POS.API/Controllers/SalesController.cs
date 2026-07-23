using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Sales;
using POS.Application.Interfaces;
using POS.Application.Platform;
using POS.Application.Sales;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.TenantCashierOrAdmin)]
public sealed class SalesController : ControllerBase
{
    private readonly ICreateSaleHandler _createSaleHandler;
    private readonly ISalesQueryService _salesQuery;

    public SalesController(ICreateSaleHandler createSaleHandler, ISalesQueryService salesQuery)
    {
        _createSaleHandler = createSaleHandler;
        _salesQuery = salesQuery;
    }

    /// <summary>Lista ventas con paginación. Fechas en UTC; rango inclusive por día calendario.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedSalesResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var paged = await _salesQuery.GetPagedAsync(startDate, endDate, pageNumber, pageSize, cancellationToken);
        return Ok(
            ApiResponse<PagedSalesResponse>.FromResult(Result<PagedSalesResponse>.Ok(paged)));
    }

    /// <summary>Resumen de ventas e importe para un día (UTC). Si no se envía <paramref name="date" />, usa hoy (UTC).</summary>
    [HttpGet("daily-summary")]
    [ProducesResponseType(typeof(ApiResponse<DailySummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailySummary(
        [FromQuery] DateTime? date,
        CancellationToken cancellationToken = default)
    {
        var summary = await _salesQuery.GetDailySummaryAsync(date, cancellationToken);
        return Ok(
            ApiResponse<DailySummaryResponse>.FromResult(Result<DailySummaryResponse>.Ok(summary)));
    }

    /// <summary>
    /// Reporte de ventas por rango (UTC, inclusive por día): totales, breakdown por medio de pago y por cajero.
    /// Sin fechas → hoy (UTC). Solo <c>startDate</c> o <c>endDate</c> → ese día.
    /// </summary>
    [HttpGet("report")]
    [ProducesResponseType(typeof(ApiResponse<SalesReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var report = await _salesQuery.GetSalesReportAsync(startDate, endDate, cancellationToken);
        return Ok(ApiResponse<SalesReportResponse>.FromResult(Result<SalesReportResponse>.Ok(report)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SaleDetailViewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SaleDetailViewResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await _salesQuery.GetByIdAsync(id, cancellationToken);
        if (detail is null)
        {
            var notFound = Result<SaleDetailViewResponse>.Failure("sale.not_found", "Venta no encontrada.");
            return NotFound(ApiResponse<SaleDetailViewResponse>.FromResult(notFound));
        }

        return Ok(
            ApiResponse<SaleDetailViewResponse>.FromResult(Result<SaleDetailViewResponse>.Ok(detail)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SaleResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<SaleResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSaleCommand(
            request.Lines
                .Select(l => new CreateSaleLineCommand(l.ProductId, l.Quantity, l.StockLotId))
                .ToList(),
            (request.Payments ?? Array.Empty<CreateSalePaymentRequest>())
                .Select(p => new CreateSalePaymentCommand(p.Method, p.Amount))
                .ToList(),
            request.CustomerId);

        var result = await _createSaleHandler.HandleAsync(command, cancellationToken);
        var body = ApiResponse<SaleResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);

        return StatusCode(StatusCodes.Status201Created, body);
    }
}
