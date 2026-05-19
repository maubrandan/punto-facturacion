using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/tenants/{tenantId}/entitlements")]
[Tags("Platform")]
public sealed class PlatformTenantEntitlementsController : ControllerBase
{
    private readonly IPlatformTenantEntitlementsService _entitlements;

    public PlatformTenantEntitlementsController(IPlatformTenantEntitlementsService entitlements) =>
        _entitlements = entitlements;

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PlatformUser)]
    [ProducesResponseType(typeof(ApiResponse<TenantEntitlementsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantEntitlementsDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string tenantId, CancellationToken cancellationToken)
    {
        var result = await _entitlements.GetAsync(tenantId, cancellationToken);
        var body = ApiResponse<TenantEntitlementsDto>.FromResult(result);
        if (!result.IsSuccess && result.ErrorCode == "tenant.not_found")
            return NotFound(body);
        return Ok(body);
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantEntitlementsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantEntitlementsDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantEntitlementsDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(
        string tenantId,
        [FromBody] SetTenantEntitlementsApiRequest request,
        CancellationToken cancellationToken)
    {
        var dto = new TenantEntitlementsDto
        {
            MaxProducts = request.MaxProducts,
            MaxTenantUsers = request.MaxTenantUsers,
            SalesEnabled = request.SalesEnabled
        };

        var result = await _entitlements.SetAsync(tenantId, dto, request.Justification, cancellationToken);
        var body = ApiResponse<TenantEntitlementsDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }
}
