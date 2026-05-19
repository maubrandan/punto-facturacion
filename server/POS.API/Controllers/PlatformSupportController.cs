using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/support")]
[Tags("Platform")]
public sealed class PlatformSupportController : ControllerBase
{
    private readonly IImpersonationSessionService _impersonation;

    public PlatformSupportController(IImpersonationSessionService impersonation) =>
        _impersonation = impersonation;

    /// <summary>Inicia JWT de corta duración en contexto tenant (soporte). No incluye <c>is_platform</c>.</summary>
    [HttpPost("impersonation/session")]
    [Authorize(Policy = AuthorizationPolicies.PlatformImpersonation)]
    [ProducesResponseType(typeof(ApiResponse<ImpersonationSessionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ImpersonationSessionResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartImpersonationSession(
        [FromBody] StartImpersonationSessionApiRequest request,
        CancellationToken cancellationToken)
    {
        var cmd = new StartImpersonationSessionCommand(request.TenantId, request.Reason, request.TtlMinutes);
        var result = await _impersonation.StartAsync(cmd, cancellationToken);
        var body = ApiResponse<ImpersonationSessionResponseDto>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return Ok(body);
    }
}
