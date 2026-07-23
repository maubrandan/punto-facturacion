using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Contracts;
using POS.Application.Billing;
using POS.Domain.Entities;

namespace POS.API.Controllers;

/// <summary>
/// Webhooks de pasarelas SaaS. Sin JWT tenant; verificación por firma del proveedor.
/// </summary>
[ApiController]
[Route("api/billing/webhooks")]
[AllowAnonymous]
[Tags("Billing")]
public sealed class BillingWebhooksController : ControllerBase
{
    private readonly IBillingWebhookProcessor _processor;

    public BillingWebhooksController(IBillingWebhookProcessor processor) => _processor = processor;

    [HttpPost("stripe")]
    [ProducesResponseType(typeof(ApiResponse<BillingWebhookResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BillingWebhookResult>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Stripe(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        var headers = Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await _processor.ProcessAsync(
            BillingProvider.Stripe,
            body,
            signature,
            headers,
            cancellationToken);

        var envelope = ApiResponse<BillingWebhookResult>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(envelope);
        return Ok(envelope);
    }

    [HttpPost("mercadopago")]
    [ProducesResponseType(typeof(ApiResponse<BillingWebhookResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BillingWebhookResult>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MercadoPago(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["x-signature"].FirstOrDefault()
            ?? Request.Headers["X-Signature"].FirstOrDefault();
        var headers = Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await _processor.ProcessAsync(
            BillingProvider.MercadoPago,
            body,
            signature,
            headers,
            cancellationToken);

        var envelope = ApiResponse<BillingWebhookResult>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(envelope);
        return Ok(envelope);
    }
}
