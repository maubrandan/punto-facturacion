using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Providers;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.TenantStockOrAdmin)]
public sealed class ProvidersController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProvidersController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProviderResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var entities = await _db.Providers
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var list = entities.Select(ProviderResponse.FromEntity).ToList();

        return Ok(
            ApiResponse<IReadOnlyList<ProviderResponse>>.FromResult(
                Result<IReadOnlyList<ProviderResponse>>.Ok(list)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProviderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProviderResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null)
        {
            var notFound = Result<ProviderResponse>.Failure("provider.not_found", "Proveedor no encontrado.");
            return NotFound(ApiResponse<ProviderResponse>.FromResult(notFound));
        }

        return Ok(
            ApiResponse<ProviderResponse>.FromResult(
                Result<ProviderResponse>.Ok(ProviderResponse.FromEntity(entity))));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProviderResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ProviderResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProviderRequest request, CancellationToken cancellationToken)
    {
        var err = Validate(request.Name, request.TaxId);
        if (err is not null)
            return BadRequest(
                ApiResponse<ProviderResponse>.FromResult(
                    Result<ProviderResponse>.Failure("provider.validation", err)));

        var entity = new Provider
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            TaxId = request.TaxId.Trim(),
            Email = request.Email?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty
        };

        _db.Providers.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(
                ApiResponse<ProviderResponse>.FromResult(
                    Result<ProviderResponse>.Failure(
                        "provider.duplicate",
                        "No se pudo crear el proveedor. Verifique datos únicos.")));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<ProviderResponse>.FromResult(Result<ProviderResponse>.Ok(ProviderResponse.FromEntity(entity))));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProviderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProviderResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ProviderResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProviderRequest request,
        CancellationToken cancellationToken)
    {
        var err = Validate(request.Name, request.TaxId);
        if (err is not null)
            return BadRequest(
                ApiResponse<ProviderResponse>.FromResult(
                    Result<ProviderResponse>.Failure("provider.validation", err)));

        var entity = await _db.Providers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null)
        {
            var notFound = Result<ProviderResponse>.Failure("provider.not_found", "Proveedor no encontrado.");
            return NotFound(ApiResponse<ProviderResponse>.FromResult(notFound));
        }

        entity.Name = request.Name.Trim();
        entity.TaxId = request.TaxId.Trim();
        entity.Email = request.Email?.Trim() ?? string.Empty;
        entity.Phone = request.Phone?.Trim() ?? string.Empty;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(
                ApiResponse<ProviderResponse>.FromResult(
                    Result<ProviderResponse>.Failure(
                        "provider.duplicate",
                        "No se pudo actualizar el proveedor.")));
        }

        return Ok(
            ApiResponse<ProviderResponse>.FromResult(Result<ProviderResponse>.Ok(ProviderResponse.FromEntity(entity))));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Providers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null)
        {
            var notFound = Result<object?>.Failure("provider.not_found", "Proveedor no encontrado.");
            return NotFound(ApiResponse<object?>.FromResult(notFound));
        }

        var hasPurchases = await _db.Purchases.AnyAsync(x => x.ProviderId == id, cancellationToken);
        if (hasPurchases)
        {
            return BadRequest(
                ApiResponse<object?>.FromResult(
                    Result<object?>.Failure(
                        "provider.in_use",
                        "No se puede eliminar: existen compras asociadas a este proveedor.")));
        }

        _db.Providers.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object?>.FromResult(Result<object?>.Ok(null)));
    }

    private static string? Validate(string name, string taxId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "El nombre es obligatorio.";
        if (string.IsNullOrWhiteSpace(taxId))
            return "El CUIT / identificador fiscal es obligatorio.";
        return null;
    }
}
