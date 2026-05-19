using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform")]
[Tags("Platform")]
public sealed class PlatformSystemController : ControllerBase
{
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PlatformHealthResponse>), StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        var data = new PlatformHealthResponse
        {
            Status = "ok",
            Service = "pos-api-platform"
        };
        return Ok(ApiResponse<PlatformHealthResponse>.FromResult(Result<PlatformHealthResponse>.Ok(data)));
    }

    [HttpGet("version")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PlatformVersionResponse>), StatusCodes.Status200OK)]
    public IActionResult Version()
    {
        var asm = Assembly.GetExecutingAssembly();
        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = !string.IsNullOrWhiteSpace(informational)
            ? informational
            : asm.GetName().Version?.ToString() ?? "unknown";

        var data = new PlatformVersionResponse
        {
            Version = version,
            AssemblyName = asm.GetName().Name ?? "POS.API"
        };
        return Ok(ApiResponse<PlatformVersionResponse>.FromResult(Result<PlatformVersionResponse>.Ok(data)));
    }
}
