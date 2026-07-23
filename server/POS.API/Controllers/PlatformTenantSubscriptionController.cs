using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/platform/tenants/{tenantId}/subscription")]
[Tags("Platform")]
public sealed class PlatformTenantSubscriptionController : ControllerBase
{
    private readonly IPlatformTenantSubscriptionService _subscriptions;

    public PlatformTenantSubscriptionController(IPlatformTenantSubscriptionService subscriptions) =>
        _subscriptions = subscriptions;

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.PlatformUser)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string tenantId, CancellationToken cancellationToken)
    {
        var result = await _subscriptions.GetAsync(tenantId, cancellationToken);
        var body = ApiResponse<TenantSubscriptionDto>.FromResult(result);
        if (!result.IsSuccess && result.ErrorCode == "tenant.not_found")
            return NotFound(body);
        return Ok(body);
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.PlatformOperations)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(
        string tenantId,
        [FromBody] UpdateTenantSubscriptionApiRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTenantSubscriptionCommand(
            tenantId,
            request.PlanCode,
            request.Status,
            request.BillingCycle,
            request.CurrentPeriodStartUtc,
            request.CurrentPeriodEndUtc,
            request.TrialEndsAtUtc,
            request.CancelAtPeriodEnd,
            request.Notes,
            request.Justification);

        var result = await _subscriptions.UpdateAsync(command, cancellationToken);
        var body = ApiResponse<TenantSubscriptionDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "tenant.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }
}
