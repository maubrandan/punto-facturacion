using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/metrics")]
[Tags("Platform")]
public sealed class PlatformMetricsController : ControllerBase
{
    private readonly IPlatformMetricsOverviewQuery _query;

    public PlatformMetricsController(IPlatformMetricsOverviewQuery query) => _query = query;

    [HttpGet("overview")]
    [Authorize(Policy = AuthorizationPolicies.PlatformUser)]
    [ProducesResponseType(typeof(ApiResponse<PlatformMetricsOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken = default)
    {
        var data = await _query.GetOverviewAsync(cancellationToken);
        return Ok(ApiResponse<PlatformMetricsOverviewDto>.FromResult(Result<PlatformMetricsOverviewDto>.Ok(data)));
    }
}
