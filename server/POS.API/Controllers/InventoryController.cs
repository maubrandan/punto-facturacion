using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Inventory;
using POS.Application.Interfaces;
using POS.Application.Inventory;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Policy = AuthorizationPolicies.TenantStockOrAdmin)]
public sealed class InventoryController : ControllerBase
{
    private readonly IAdjustStockHandler _adjustHandler;
    private readonly IInventoryQueryService _queries;

    public InventoryController(IAdjustStockHandler adjustHandler, IInventoryQueryService queries)
    {
        _adjustHandler = adjustHandler;
        _queries = queries;
    }

    [HttpPost("adjustments")]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StockAdjustmentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Adjust(
        [FromBody] AdjustStockApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adjustHandler.HandleAsync(
            new AdjustStockCommand(
                request.ProductId,
                request.QuantityDelta,
                request.Reason ?? string.Empty,
                request.StockLotId,
                request.LotNumber,
                request.ExpirationDate),
            cancellationToken);

        var body = ApiResponse<StockAdjustmentResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);

        return Ok(body);
    }

    [HttpGet("movements")]
    [ProducesResponseType(typeof(ApiResponse<PagedStockMovementsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovements(
        [FromQuery] Guid? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _queries.GetMovementsAsync(productId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedStockMovementsResponse>.FromResult(result));
    }

    [HttpGet("products/{productId:guid}/lots")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StockLotResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StockLotResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLots(Guid productId, CancellationToken cancellationToken)
    {
        var result = await _queries.GetLotsForProductAsync(productId, cancellationToken);
        var body = ApiResponse<IReadOnlyList<StockLotResponse>>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "product.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    public sealed class AdjustStockApiRequest
    {
        public Guid ProductId { get; init; }

        public decimal QuantityDelta { get; init; }

        public string? Reason { get; init; }

        public Guid? StockLotId { get; init; }

        public string? LotNumber { get; init; }

        public DateOnly? ExpirationDate { get; init; }
    }
}
