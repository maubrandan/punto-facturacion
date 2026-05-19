using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/tenants/{tenantId}/users")]
[Tags("Platform")]
public sealed class PlatformTenantUsersController : ControllerBase
{
    private readonly IPlatformTenantUserQuery _query;
    private readonly IPlatformTenantUserAdminService _admin;

    public PlatformTenantUsersController(
        IPlatformTenantUserQuery query,
        IPlatformTenantUserAdminService admin)
    {
        _query = query;
        _admin = admin;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PlatformUser)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserDirectoryPageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserDirectoryPageDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        string tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? emailContains = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            page = 1;
        if (pageSize < 1)
            pageSize = 20;
        if (pageSize > 100)
            pageSize = 100;

        var result = await _query.ListUsersAsync(tenantId, page, pageSize, emailContains, cancellationToken);
        var body = ApiResponse<TenantUserDirectoryPageDto>.FromResult(result);
        if (!result.IsSuccess && result.ErrorCode == "tenant.not_found")
            return NotFound(body);
        return Ok(body);
    }

    [HttpPost("{userId}/block")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Block(
        string tenantId,
        string userId,
        [FromBody] PlatformUserActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _admin.BlockAsync(tenantId, userId, request.Justification, cancellationToken);
        var body = ApiResponse<TenantUserSummaryDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found" || result.ErrorCode == "platform.users.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    [HttpPost("{userId}/unblock")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserSummaryDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unblock(
        string tenantId,
        string userId,
        [FromBody] PlatformUserActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _admin.UnblockAsync(tenantId, userId, request.Justification, cancellationToken);
        var body = ApiResponse<TenantUserSummaryDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found" || result.ErrorCode == "platform.users.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    [HttpPost("{userId}/request-password-reset")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<PlatformMutationAckDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlatformMutationAckDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PlatformMutationAckDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestPasswordReset(
        string tenantId,
        string userId,
        [FromBody] PlatformUserActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _admin.RequestPasswordResetAsync(tenantId, userId, request.Justification, cancellationToken);
        var body = ApiResponse<PlatformMutationAckDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found" || result.ErrorCode == "platform.users.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    [HttpPost("{userId}/resend-email-confirmation")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<PlatformMutationAckDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PlatformMutationAckDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PlatformMutationAckDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendEmailConfirmation(
        string tenantId,
        string userId,
        [FromBody] PlatformUserActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _admin.ResendEmailConfirmationAsync(tenantId, userId, request.Justification, cancellationToken);
        var body = ApiResponse<PlatformMutationAckDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found" || result.ErrorCode == "platform.users.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }
}
