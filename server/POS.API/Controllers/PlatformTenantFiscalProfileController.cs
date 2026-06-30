using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Fiscal;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/tenants/{tenantId}/fiscal-profile")]
[Tags("Platform")]
public sealed class PlatformTenantFiscalProfileController : ControllerBase
{
    private readonly IPlatformTenantFiscalProfileService _fiscalProfile;

    public PlatformTenantFiscalProfileController(IPlatformTenantFiscalProfileService fiscalProfile) =>
        _fiscalProfile = fiscalProfile;

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PlatformUser)]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string tenantId, CancellationToken cancellationToken)
    {
        var result = await _fiscalProfile.GetAsync(tenantId, cancellationToken);
        var body = ApiResponse<TenantFiscalProfileResponse>.FromResult(result);
        if (!result.IsSuccess)
            return result.ErrorCode is "tenant.not_found" or "fiscal.profile_not_found" ? NotFound(body) : BadRequest(body);
        return Ok(body);
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantFiscalProfileResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(
        string tenantId,
        [FromBody] SetTenantFiscalProfileApiRequest request,
        CancellationToken cancellationToken)
    {
        var values = new UpsertTenantFiscalProfileRequest
        {
            TaxId = request.TaxId,
            PointOfSale = request.PointOfSale,
            IsProduction = request.IsProduction,
            IsEnabled = request.IsEnabled,
            CertificateRef = request.CertificateRef,
            PrivateKeyRef = request.PrivateKeyRef
        };

        var result = await _fiscalProfile.UpsertAsync(tenantId, values, request.Justification, cancellationToken);
        var body = ApiResponse<TenantFiscalProfileResponse>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }
}
