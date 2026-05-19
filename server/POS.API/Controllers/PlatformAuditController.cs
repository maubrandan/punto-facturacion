using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/audit")]
[Tags("Platform")]
public sealed class PlatformAuditController : ControllerBase
{
    private readonly IPlatformAuditQueryService _query;

    public PlatformAuditController(IPlatformAuditQueryService query) => _query = query;

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PlatformUser)]
    [ProducesResponseType(typeof(ApiResponse<PlatformAuditEventPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? actorUserId = null,
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

        var filter = new PlatformAuditListFilter
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            CreatedFromUtc = createdFromUtc,
            CreatedToUtc = createdToUtc
        };
        var data = await _query.GetPageAsync(page, pageSize, filter, cancellationToken);
        return Ok(ApiResponse<PlatformAuditEventPageDto>.FromResult(Result<PlatformAuditEventPageDto>.Ok(data)));
    }
}
