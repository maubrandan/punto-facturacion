using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Purchases;
using POS.Application.Interfaces;
using POS.Application.Platform;
using POS.Application.Purchases;
using POS.Infrastructure.Persistence;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.TenantStockOrAdmin)]
public sealed class PurchasesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICreatePurchaseHandler _createPurchaseHandler;

    public PurchasesController(ApplicationDbContext db, ICreatePurchaseHandler createPurchaseHandler)
    {
        _db = db;
        _createPurchaseHandler = createPurchaseHandler;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PurchaseSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        var rows = await (
            from pu in _db.Purchases.AsNoTracking()
            join pr in _db.Providers.AsNoTracking() on pu.ProviderId equals pr.Id
            orderby pu.Date descending
            select new PurchaseSummaryResponse
            {
                Id = pu.Id,
                ProviderId = pu.ProviderId,
                ProviderName = pr.Name,
                Date = pu.Date,
                InvoiceNumber = pu.InvoiceNumber,
                Total = pu.Total
            }).ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<IReadOnlyList<PurchaseSummaryResponse>>.FromResult(
                Result<IReadOnlyList<PurchaseSummaryResponse>>.Ok(rows)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var purchase = await _db.Purchases
            .AsNoTracking()
            .Include(p => p.Provider)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (purchase is null || purchase.Provider is null)
        {
            var notFound = Result<PurchaseResponse>.Failure("purchase.not_found", "Compra no encontrada.");
            return NotFound(ApiResponse<PurchaseResponse>.FromResult(notFound));
        }

        var lines = await _db.PurchaseDetails
            .AsNoTracking()
            .Where(d => d.PurchaseId == id)
            .OrderBy(d => d.ProductName)
            .Select(
                d => new PurchaseLineResponse
                {
                    Id = d.Id,
                    ProductId = d.ProductId,
                    ProductName = d.ProductName,
                    ProductSku = d.ProductSku,
                    Quantity = d.Quantity,
                    UnitCost = d.UnitCost,
                    Subtotal = d.Subtotal,
                    LotNumberSnapshot = d.LotNumberSnapshot,
                    ExpirationSnapshot = d.ExpirationSnapshot
                })
            .ToListAsync(cancellationToken);

        var response = new PurchaseResponse
        {
            Id = purchase.Id,
            ProviderId = purchase.ProviderId,
            ProviderName = purchase.Provider.Name,
            Date = purchase.Date,
            InvoiceNumber = purchase.InvoiceNumber,
            Total = purchase.Total,
            Lines = lines
        };

        return Ok(
            ApiResponse<PurchaseResponse>.FromResult(Result<PurchaseResponse>.Ok(response)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PurchaseResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreatePurchaseCommand(
            request.ProviderId,
            request.Date,
            request.InvoiceNumber ?? string.Empty,
            request.Lines
                .Select(l => new CreatePurchaseLineCommand(
                    l.ProductId,
                    l.Quantity,
                    l.UnitCost,
                    l.LotNumber,
                    l.ExpirationDate))
                .ToList());

        var result = await _createPurchaseHandler.HandleAsync(command, cancellationToken);
        var body = ApiResponse<PurchaseResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);

        return StatusCode(StatusCodes.Status201Created, body);
    }
}
