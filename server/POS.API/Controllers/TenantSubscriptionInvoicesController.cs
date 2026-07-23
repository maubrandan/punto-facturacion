using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Application.Platform;

namespace POS.API.Controllers;

[ApiController]
[Route("api/tenant/invoices")]
[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
public sealed class TenantSubscriptionInvoicesController : ControllerBase
{
    private readonly ITenantSubscriptionInvoiceQuery _query;

    public TenantSubscriptionInvoicesController(ITenantSubscriptionInvoiceQuery query) => _query = query;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionInvoiceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionInvoiceListDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _query.ListForCurrentTenantAsync(page, pageSize, cancellationToken);
        var body = ApiResponse<SubscriptionInvoiceListDto>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);
        return Ok(body);
    }

    [HttpGet("{invoiceId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionInvoiceDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionInvoiceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(Guid invoiceId, CancellationToken cancellationToken)
    {
        var result = await _query.GetForCurrentTenantAsync(invoiceId, cancellationToken);
        var body = ApiResponse<SubscriptionInvoiceDto>.FromResult(result);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "invoice.not_found")
                return NotFound(body);
            return BadRequest(body);
        }

        return Ok(body);
    }
}
