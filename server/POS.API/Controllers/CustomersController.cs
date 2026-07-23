using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Customers;
using POS.Application.Interfaces;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICustomerAccountQueryService _accountQuery;
    private readonly IRegisterCustomerAccountPaymentHandler _registerPayment;

    public CustomersController(
        ApplicationDbContext db,
        ICustomerAccountQueryService accountQuery,
        IRegisterCustomerAccountPaymentHandler registerPayment)
    {
        _db = db;
        _accountQuery = accountQuery;
        _registerPayment = registerPayment;
    }

    /// <summary>Listado / búsqueda (cajero y admin para Factura A).</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.TenantCashierOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term)
                || c.TaxId.Contains(term)
                || (c.Email != null && c.Email.ToLower().Contains(term)));
        }

        var entities = await query
            .OrderBy(c => c.Name)
            .Take(100)
            .ToListAsync(cancellationToken);

        var list = entities.Select(CustomerResponse.FromEntity).ToList();
        return Ok(
            ApiResponse<IReadOnlyList<CustomerResponse>>.FromResult(
                Result<IReadOnlyList<CustomerResponse>>.Ok(list)));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantCashierOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            var notFound = Result<CustomerResponse>.Failure("customer.not_found", "Cliente no encontrado.");
            return NotFound(ApiResponse<CustomerResponse>.FromResult(notFound));
        }

        return Ok(
            ApiResponse<CustomerResponse>.FromResult(
                Result<CustomerResponse>.Ok(CustomerResponse.FromEntity(entity))));
    }

    /// <summary>Saldo de cuenta corriente y movimientos recientes.</summary>
    [HttpGet("{id:guid}/account")]
    [Authorize(Policy = AuthorizationPolicies.TenantCashierOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<CustomerAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CustomerAccountResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount(Guid id, CancellationToken cancellationToken)
    {
        var result = await _accountQuery.GetAccountAsync(id, cancellationToken: cancellationToken);
        var body = ApiResponse<CustomerAccountResponse>.FromResult(result);
        if (!result.IsSuccess)
            return NotFound(body);
        return Ok(body);
    }

    /// <summary>Historial de movimientos de cuenta corriente.</summary>
    [HttpGet("{id:guid}/movements")]
    [Authorize(Policy = AuthorizationPolicies.TenantCashierOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerAccountMovementResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerAccountMovementResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMovements(Guid id, CancellationToken cancellationToken)
    {
        var result = await _accountQuery.ListMovementsAsync(id, cancellationToken);
        var body = ApiResponse<IReadOnlyList<CustomerAccountMovementResponse>>.FromResult(result);
        if (!result.IsSuccess)
            return NotFound(body);
        return Ok(body);
    }

    /// <summary>Registra un cobro / pago de deuda en cuenta corriente.</summary>
    [HttpPost("{id:guid}/account/payments")]
    [Authorize(Policy = AuthorizationPolicies.TenantCashierOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<RegisterCustomerAccountPaymentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<RegisterCustomerAccountPaymentResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<RegisterCustomerAccountPaymentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterPayment(
        Guid id,
        [FromBody] RegisterCustomerAccountPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _registerPayment.HandleAsync(id, request, cancellationToken);
        var body = ApiResponse<RegisterCustomerAccountPaymentResponse>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "customer.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return StatusCode(StatusCodes.Status201Created, body);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var err = Validate(request.Name, request.TaxId);
        if (err is not null)
        {
            return BadRequest(
                ApiResponse<CustomerResponse>.FromResult(
                    Result<CustomerResponse>.Failure("customer.validation", err)));
        }

        var entity = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            TaxId = NormalizeTaxId(request.TaxId),
            Email = request.Email?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty,
            Address = request.Address?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(
                ApiResponse<CustomerResponse>.FromResult(
                    Result<CustomerResponse>.Failure(
                        "customer.duplicate",
                        "Ya existe un cliente con ese CUIT/documento en este negocio.")));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CustomerResponse>.FromResult(
                Result<CustomerResponse>.Ok(CustomerResponse.FromEntity(entity))));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var err = Validate(request.Name, request.TaxId);
        if (err is not null)
        {
            return BadRequest(
                ApiResponse<CustomerResponse>.FromResult(
                    Result<CustomerResponse>.Failure("customer.validation", err)));
        }

        var entity = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound(
                ApiResponse<CustomerResponse>.FromResult(
                    Result<CustomerResponse>.Failure("customer.not_found", "Cliente no encontrado.")));
        }

        entity.Name = request.Name.Trim();
        entity.TaxId = NormalizeTaxId(request.TaxId);
        entity.Email = request.Email?.Trim() ?? string.Empty;
        entity.Phone = request.Phone?.Trim() ?? string.Empty;
        entity.Address = request.Address?.Trim() ?? string.Empty;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(
                ApiResponse<CustomerResponse>.FromResult(
                    Result<CustomerResponse>.Failure(
                        "customer.duplicate",
                        "Ya existe un cliente con ese CUIT/documento en este negocio.")));
        }

        return Ok(
            ApiResponse<CustomerResponse>.FromResult(
                Result<CustomerResponse>.Ok(CustomerResponse.FromEntity(entity))));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound(
                ApiResponse<object?>.FromResult(
                    Result<object?>.Failure("customer.not_found", "Cliente no encontrado.")));
        }

        _db.Customers.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object?>.FromResult(Result<object?>.Ok(null)));
    }

    private static string? Validate(string name, string taxId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "El nombre es obligatorio.";
        if (string.IsNullOrWhiteSpace(taxId))
            return "El CUIT/documento es obligatorio.";
        var digits = NormalizeTaxId(taxId);
        if (digits.Length is < 7 or > 11)
            return "El documento debe tener entre 7 y 11 dígitos.";
        return null;
    }

    private static string NormalizeTaxId(string taxId) =>
        new(taxId.Where(char.IsDigit).ToArray());
}
