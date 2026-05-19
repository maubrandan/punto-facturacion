using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Products;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantEntitlementGuard _entitlements;

    public ProductsController(ApplicationDbContext db, ITenantEntitlementGuard entitlements)
    {
        _db = db;
        _entitlements = entitlements;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        // El filtro global multi-tenant de DbContext aplica automáticamente por TenantId.
        var entities = await _db.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var data = entities.Select(ProductResponse.FromEntity).ToList();
        var result = Result<IReadOnlyList<ProductResponse>>.Ok(data);
        return Ok(ApiResponse<IReadOnlyList<ProductResponse>>.FromResult(result));
    }

    /// <summary>Productos con menor stock del tenant (por defecto 5, máx. 20).</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStock(
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        if (count < 1) count = 1;
        if (count > 20) count = 20;

        var entities = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Stock)
            .ThenBy(p => p.Name)
            .Take(count)
            .ToListAsync(cancellationToken);

        var data = entities.Select(ProductResponse.FromEntity).ToList();
        var result = Result<IReadOnlyList<ProductResponse>>.Ok(data);
        return Ok(ApiResponse<IReadOnlyList<ProductResponse>>.FromResult(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            var notFound = Result<ProductResponse>.Failure("product.not_found", "Producto no encontrado.");
            return NotFound(ApiResponse<ProductResponse>.FromResult(notFound));
        }

        var result = Result<ProductResponse>.Ok(ProductResponse.FromEntity(product));
        return Ok(ApiResponse<ProductResponse>.FromResult(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var creation = await CreateInternalAsync(request, cancellationToken);
        var body = ApiResponse<ProductResponse>.FromResult(creation);
        if (!creation.IsSuccess)
            return BadRequest(body);

        return StatusCode(StatusCodes.Status201Created, body);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var update = await UpdateInternalAsync(id, request, cancellationToken);
        var body = ApiResponse<ProductResponse>.FromResult(update);
        if (!update.IsSuccess)
        {
            if (update.ErrorCode == "product.not_found")
                return NotFound(body);

            return BadRequest(body);
        }

        return Ok(body);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deletion = await DeleteInternalAsync(id, cancellationToken);
        var body = ApiResponse<object?>.FromResult(deletion);
        if (!deletion.IsSuccess)
            return NotFound(body);

        return Ok(body);
    }

    private async Task<Result<ProductResponse>> CreateInternalAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateProductInput(
            request.Name,
            request.SKU,
            request.NetPrice,
            request.TaxRate,
            request.Stock);
        if (validationError is not null)
            return Result<ProductResponse>.Failure("product.validation", validationError);

        var quotaCheck = await _entitlements.EnsureCanCreateProductAsync(cancellationToken);
        if (!quotaCheck.IsSuccess)
        {
            return Result<ProductResponse>.Failure(
                quotaCheck.ErrorCode!,
                quotaCheck.Error!);
        }

        var extendedDataJson = ResolveExtendedDataJson(request.ExtendedDataJson);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            SKU = request.SKU.Trim(),
            Barcode = request.Barcode.Trim(),
            NetPrice = request.NetPrice,
            TaxRate = request.TaxRate,
            Stock = request.Stock,
            ExtendedDataJson = extendedDataJson,
            CreatedAt = DateTime.UtcNow
        };

        // TenantId NO se recibe del cliente: lo asigna Infrastructure en SaveChanges usando ICurrentUserService.
        _db.Products.Add(product);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<ProductResponse>.Failure(
                "product.duplicate",
                "No se pudo crear el producto. Verifique que el SKU no esté repetido para este tenant.");
        }

        return Result<ProductResponse>.Ok(ProductResponse.FromEntity(product));
    }

    private async Task<Result<ProductResponse>> UpdateInternalAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateProductInput(
            request.Name,
            request.SKU,
            request.NetPrice,
            request.TaxRate,
            request.Stock);
        if (validationError is not null)
            return Result<ProductResponse>.Failure("product.validation", validationError);

        // Multi-tenant: con query filter global, si no coincide tenant también devuelve null (NotFound).
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
            return Result<ProductResponse>.Failure("product.not_found", "Producto no encontrado.");

        product.Name = request.Name.Trim();
        product.SKU = request.SKU.Trim();
        product.Barcode = request.Barcode.Trim();
        product.NetPrice = request.NetPrice;
        product.TaxRate = request.TaxRate;
        product.Stock = request.Stock;
        product.ExtendedDataJson = ResolveExtendedDataJson(request.ExtendedDataJson);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<ProductResponse>.Failure(
                "product.duplicate",
                "No se pudo actualizar el producto. Verifique que el SKU no esté repetido para este tenant.");
        }

        return Result<ProductResponse>.Ok(ProductResponse.FromEntity(product));
    }

    private static string? ValidateProductInput(
        string name,
        string sku,
        decimal netPrice,
        decimal taxRate,
        int stock)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "El nombre es obligatorio.";

        if (string.IsNullOrWhiteSpace(sku))
            return "El SKU es obligatorio.";

        if (netPrice < 0m)
            return "NetPrice no puede ser negativo.";

        if (taxRate < 0m)
            return "TaxRate no puede ser negativo.";

        return null;
    }

    private static string ResolveExtendedDataJson(JsonElement? value)
    {
        if (value is null || value.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return "{}";

        return value.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()?.Trim() ?? "{}"
            : value.Value.GetRawText();
    }

    private async Task<Result<object?>> DeleteInternalAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
            return Result<object?>.Failure("product.not_found", "Producto no encontrado.");

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<object?>.Ok(null);
    }

    public sealed class CreateProductRequest
    {
        public required string Name { get; init; }

        public required string SKU { get; init; }

        public string Barcode { get; init; } = string.Empty;

        public decimal NetPrice { get; init; }

        public decimal TaxRate { get; init; }

        public int Stock { get; init; }

        public JsonElement? ExtendedDataJson { get; init; }
    }

    public sealed class UpdateProductRequest
    {
        public required string Name { get; init; }

        public required string SKU { get; init; }

        public string Barcode { get; init; } = string.Empty;

        public decimal NetPrice { get; init; }

        public decimal TaxRate { get; init; }

        public int Stock { get; init; }

        public JsonElement? ExtendedDataJson { get; init; }
    }
}
