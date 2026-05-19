using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Auth;
using POS.Application.Interfaces;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/auth")]
[Tags("Platform")]
public sealed class PlatformAuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public PlatformAuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.PlatformLoginAsync(request, cancellationToken);
        var body = ApiResponse<AuthResponse>.FromResult(result);
        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "auth.platform.locked" => StatusCode(StatusCodes.Status423Locked, body),
                "auth.platform.not_platform_user" => BadRequest(body),
                "auth.platform.no_roles" => StatusCode(StatusCodes.Status403Forbidden, body),
                _ => Unauthorized(body),
            };
        }

        return Ok(body);
    }
}
