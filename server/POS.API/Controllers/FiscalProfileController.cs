using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Fiscal;
using POS.Application.Interfaces;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.API.Controllers;

[ApiController]
[Route("api/fiscal/profile")]
[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
public sealed class FiscalProfileController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FiscalProfileController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var profile = await _db.TenantFiscalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return NotFound(
                ApiResponse<TenantFiscalProfileResponse>.FromResult(
                    Result<TenantFiscalProfileResponse>.Failure(
                        "fiscal.profile_not_found",
                        "No hay perfil fiscal configurado para este negocio.")));
        }

        return Ok(
            ApiResponse<TenantFiscalProfileResponse>.FromResult(
                Result<TenantFiscalProfileResponse>.Ok(TenantFiscalProfileResponse.FromEntity(profile))));
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertTenantFiscalProfileRequest request,
        CancellationToken cancellationToken)
    {
        var err = Validate(request);
        if (err is not null)
        {
            return BadRequest(
                ApiResponse<TenantFiscalProfileResponse>.FromResult(
                    Result<TenantFiscalProfileResponse>.Failure("fiscal.profile_validation", err)));
        }

        var tenantId = RequireTenantId();
        var now = DateTime.UtcNow;
        var taxId = new string(request.TaxId.Where(char.IsDigit).ToArray());

        var profile = await _db.TenantFiscalProfiles
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

        if (profile is null)
        {
            profile = new TenantFiscalProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CreatedAtUtc = now
            };
            _db.TenantFiscalProfiles.Add(profile);
        }

        profile.TaxId = taxId;
        profile.PointOfSale = request.PointOfSale;
        profile.IsProduction = request.IsProduction;
        profile.IsEnabled = request.IsEnabled;
        profile.CertificateRef = request.CertificateRef.Trim();
        profile.PrivateKeyRef = request.PrivateKeyRef.Trim();
        profile.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(
            ApiResponse<TenantFiscalProfileResponse>.FromResult(
                Result<TenantFiscalProfileResponse>.Ok(TenantFiscalProfileResponse.FromEntity(profile))));
    }

    private string RequireTenantId()
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new InvalidOperationException("No se pudo resolver el tenant actual.");
        return tenantId;
    }

    private static string? Validate(UpsertTenantFiscalProfileRequest request)
    {
        var taxId = new string(request.TaxId.Where(char.IsDigit).ToArray());
        if (taxId.Length != 11)
            return "El CUIT debe tener 11 dígitos.";
        if (request.PointOfSale < 1 || request.PointOfSale > 99999)
            return "El punto de venta debe estar entre 1 y 99999.";
        return null;
    }
}
