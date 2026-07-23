using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/operators")]
[Tags("Platform")]
[Authorize(Policy = AuthorizationPolicies.PlatformSuperAdmin)]
public sealed class PlatformOperatorsController : ControllerBase
{
    private readonly IPlatformOperatorQuery _query;
    private readonly IPlatformOperatorAdminService _admin;

    public PlatformOperatorsController(
        IPlatformOperatorQuery query,
        IPlatformOperatorAdminService admin)
    {
        _query = query;
        _admin = admin;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorDirectoryPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? emailContains = null,
        [FromQuery] string? role = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            page = 1;
        if (pageSize < 1)
            pageSize = 20;
        if (pageSize > 100)
            pageSize = 100;

        var data = await _query.ListAsync(page, pageSize, emailContains, role, cancellationToken);
        return Ok(ApiResponse<PlatformOperatorDirectoryPageDto>.FromResult(
            Result<PlatformOperatorDirectoryPageDto>.Ok(data)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Provision(
        [FromBody] ProvisionPlatformOperatorApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _admin.ProvisionAsync(
            new ProvisionPlatformUserCommand(
                request.Email,
                request.Password,
                request.FullName,
                request.PlatformRole),
            cancellationToken);
        return ToMutationResponse(result);
    }

    [HttpPatch("{userId}")]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string userId,
        [FromBody] UpdatePlatformOperatorApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _admin.UpdateAsync(
            new UpdatePlatformOperatorCommand(userId, request.FullName, request.PlatformRole),
            cancellationToken);
        return ToMutationResponse(result);
    }

    [HttpPost("{userId}/block")]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Block(
        string userId,
        [FromBody] PlatformUserActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _admin.BlockAsync(
            new BlockPlatformOperatorCommand(userId, request.Justification),
            cancellationToken);
        return ToMutationResponse(result);
    }

    [HttpPost("{userId}/unblock")]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PlatformOperatorSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unblock(
        string userId,
        [FromBody] PlatformUserActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _admin.UnblockAsync(
            new UnblockPlatformOperatorCommand(userId, request.Justification),
            cancellationToken);
        return ToMutationResponse(result);
    }

    private IActionResult ToMutationResponse(Result<PlatformOperatorSummaryDto> result)
    {
        var body = ApiResponse<PlatformOperatorSummaryDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "platform.operators.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }
}
