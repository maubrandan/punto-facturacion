using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/tenants")]
[Tags("Platform")]
public sealed class PlatformTenantsController : ControllerBase
{
    private readonly IPlatformDirectoryQuery _directory;
    private readonly IPlatformTenantLifecycleService _lifecycle;

    public PlatformTenantsController(
        IPlatformDirectoryQuery directory,
        IPlatformTenantLifecycleService lifecycle)
    {
        _directory = directory;
        _lifecycle = lifecycle;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PlatformUser)]
    [ProducesResponseType(typeof(ApiResponse<TenantDirectoryPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? nameContains = null,
        [FromQuery] TenantStatus? status = null,
        [FromQuery] DateTime? createdFromUtc = null,
        [FromQuery] DateTime? createdToUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            page = 1;
        if (pageSize < 1)
            pageSize = 20;
        if (pageSize > 100)
            pageSize = 100;

        var filter = new TenantListFilter
        {
            NameContains = nameContains,
            Status = status,
            CreatedFromUtc = createdFromUtc,
            CreatedToUtc = createdToUtc
        };

        var data = await _directory.ListTenantsPageAsync(page, pageSize, filter, cancellationToken);
        return Ok(ApiResponse<TenantDirectoryPageDto>.FromResult(Result<TenantDirectoryPageDto>.Ok(data)));
    }

    [HttpGet("{tenantId}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformUser)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string tenantId, CancellationToken cancellationToken)
    {
        var dto = await _directory.GetTenantByIdAsync(tenantId, cancellationToken);
        if (dto is null)
        {
            var fail = Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");
            return NotFound(ApiResponse<TenantDetailDto>.FromResult(fail));
        }

        return Ok(ApiResponse<TenantDetailDto>.FromResult(Result<TenantDetailDto>.Ok(dto)));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantApiRequest request,
        CancellationToken cancellationToken)
    {
        var cmd = new CreatePlatformTenantCommand(request.Name, request.ContactEmail, request.BusinessType);
        var result = await _lifecycle.CreateAsync(cmd, cancellationToken);
        var body = ApiResponse<TenantDetailDto>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return Ok(body);
    }

    [HttpPatch("{tenantId}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string tenantId,
        [FromBody] UpdateTenantApiRequest request,
        CancellationToken cancellationToken)
    {
        var cmd = new UpdatePlatformTenantCommand(tenantId, request.Name, request.ContactEmail);
        var result = await _lifecycle.UpdateAsync(cmd, cancellationToken);
        var body = ApiResponse<TenantDetailDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    [HttpPost("{tenantId}/suspend")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(string tenantId, CancellationToken cancellationToken)
    {
        var result = await _lifecycle.SuspendAsync(new SuspendPlatformTenantCommand(tenantId), cancellationToken);
        var body = ApiResponse<TenantDetailDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    [HttpPost("{tenantId}/close")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Close(string tenantId, CancellationToken cancellationToken)
    {
        var result = await _lifecycle.CloseAsync(new ClosePlatformTenantCommand(tenantId), cancellationToken);
        var body = ApiResponse<TenantDetailDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }
}
