using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Contracts;
using POS.Application.Contracts.Auth;
using POS.Application.Interfaces;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        var body = ApiResponse<AuthResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return Ok(body);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        var body = ApiResponse<AuthResponse>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "auth.login.locked")
                return StatusCode(StatusCodes.Status423Locked, body);
            if (result.ErrorCode is "auth.login.tenant_suspended"
                or "auth.login.tenant_closed"
                or "auth.login.platform_blocked")
                return StatusCode(StatusCodes.Status403Forbidden, body);
            return Unauthorized(body);
        }

        return Ok(body);
    }

    [HttpPost("confirm-email")]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ConfirmEmailAsync(request, cancellationToken);
        var body = ApiResponse<AuthMessageResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return Ok(body);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(request, cancellationToken);
        var body = ApiResponse<AuthMessageResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return Ok(body);
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthMessageResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ForgotPasswordAsync(request, cancellationToken);
        var body = ApiResponse<AuthMessageResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return Ok(body);
    }
}
