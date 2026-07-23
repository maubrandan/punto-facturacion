using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Billing;
using POS.Application.Contracts;
using POS.Application.Contracts.Billing;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Application.Platform;

namespace POS.API.Controllers;

/// <summary>Suscripción SaaS del tenant actual (lectura + self-serve upgrade).</summary>
[ApiController]
[Route("api/tenant/subscription")]
[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
public sealed class TenantSubscriptionController : ControllerBase
{
    private readonly ITenantSubscriptionQuery _query;
    private readonly ISelfServeUpgradeSubscriptionHandler _upgrade;

    public TenantSubscriptionController(
        ITenantSubscriptionQuery query,
        ISelfServeUpgradeSubscriptionHandler upgrade)
    {
        _query = query;
        _upgrade = upgrade;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _query.GetForCurrentTenantAsync(cancellationToken);
        var body = ApiResponse<TenantSubscriptionDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }

    /// <summary>Upgrade self-serve (Tenant.Admin). Downgrade no permitido.</summary>
    [HttpPost("upgrade")]
    [ProducesResponseType(typeof(ApiResponse<SelfServeUpgradeResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SelfServeUpgradeResultDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upgrade(
        [FromBody] SelfServeUpgradeApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _upgrade.HandleAsync(
            new SelfServeUpgradeSubscriptionCommand(
                request.PlanCode,
                request.BillingCycle,
                request.SuccessUrl,
                request.CancelUrl),
            cancellationToken);

        var body = ApiResponse<SelfServeUpgradeResultDto>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return Ok(body);
    }
}
