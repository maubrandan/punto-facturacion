using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.TenantUsers;
using POS.Application.Interfaces;
using POS.Application.Platform;
using POS.Application.TenantUsers;

namespace POS.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
public sealed class TenantUsersController : ControllerBase
{
    private readonly ICreateTenantUserHandler _create;
    private readonly IUpdateTenantUserHandler _update;
    private readonly ISetTenantUserBlockedHandler _block;
    private readonly IListTenantUsersQuery _list;
    private readonly IRequestTenantUserPasswordResetHandler _reset;

    public TenantUsersController(
        ICreateTenantUserHandler create,
        IUpdateTenantUserHandler update,
        ISetTenantUserBlockedHandler block,
        IListTenantUsersQuery list,
        IRequestTenantUserPasswordResetHandler reset)
    {
        _create = create;
        _update = update;
        _block = block;
        _list = list;
        _reset = reset;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TenantUserDirectoryPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? emailContains = null,
        CancellationToken cancellationToken = default)
    {
        var data = await _list.ListAsync(page, pageSize, emailContains, cancellationToken);
        return Ok(ApiResponse<TenantUserDirectoryPageDto>.FromResult(Result<TenantUserDirectoryPageDto>.Ok(data)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TenantUserListItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserListItemDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantUserApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _create.HandleAsync(
            new CreateTenantUserCommand(
                request.Email,
                request.Password,
                request.FullName ?? string.Empty,
                request.Role),
            cancellationToken);
        var body = ApiResponse<TenantUserListItemDto>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return StatusCode(StatusCodes.Status201Created, body);
    }

    [HttpPut("{userId}")]
    [ProducesResponseType(typeof(ApiResponse<TenantUserListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserListItemDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantUserListItemDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string userId,
        [FromBody] UpdateTenantUserApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _update.HandleAsync(
            new UpdateTenantUserCommand(userId, request.FullName, request.Role),
            cancellationToken);
        var body = ApiResponse<TenantUserListItemDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.users.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    [HttpPost("{userId}/block")]
    [ProducesResponseType(typeof(ApiResponse<TenantUserListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Block(string userId, CancellationToken cancellationToken)
    {
        var result = await _block.HandleAsync(new SetTenantUserBlockedCommand(userId, true), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{userId}/unblock")]
    [ProducesResponseType(typeof(ApiResponse<TenantUserListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unblock(string userId, CancellationToken cancellationToken)
    {
        var result = await _block.HandleAsync(new SetTenantUserBlockedCommand(userId, false), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{userId}/request-password-reset")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestPasswordReset(string userId, CancellationToken cancellationToken)
    {
        var result = await _reset.HandleAsync(userId, cancellationToken);
        var body = ApiResponse<object?>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.users.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    private IActionResult ToActionResult(Result<TenantUserListItemDto> result)
    {
        var body = ApiResponse<TenantUserListItemDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.users.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }
}
