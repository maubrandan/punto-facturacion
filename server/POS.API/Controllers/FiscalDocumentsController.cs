using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Fiscal;
using POS.Application.Fiscal;
using POS.Application.Interfaces;

namespace POS.API.Controllers;

[ApiController]
[Route("api/fiscal-documents")]
[Authorize]
public sealed class FiscalDocumentsController : ControllerBase
{
    private readonly IIssueElectronicInvoiceHandler _issueHandler;
    private readonly IRetryElectronicInvoiceHandler _retryHandler;
    private readonly IIssueCreditNoteHandler _creditNoteHandler;

    public FiscalDocumentsController(
        IIssueElectronicInvoiceHandler issueHandler,
        IRetryElectronicInvoiceHandler retryHandler,
        IIssueCreditNoteHandler creditNoteHandler)
    {
        _issueHandler = issueHandler;
        _retryHandler = retryHandler;
        _creditNoteHandler = creditNoteHandler;
    }

    [HttpPost("issue")]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Issue(
        [FromBody] IssueElectronicInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _issueHandler.HandleAsync(
            new IssueElectronicInvoiceCommand(request.SaleId, request.IsInvoiceA),
            cancellationToken);
        var body = ApiResponse<FiscalDocumentResponse>.FromResult(result);
        return result.IsSuccess ? Ok(body) : BadRequest(body);
    }

    [HttpPost("retry")]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Retry(
        [FromBody] RetryElectronicInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _retryHandler.HandleAsync(
            new RetryElectronicInvoiceCommand(request.FiscalDocumentId),
            cancellationToken);
        var body = ApiResponse<FiscalDocumentResponse>.FromResult(result);
        return result.IsSuccess ? Ok(body) : BadRequest(body);
    }

    [HttpPost("credit-note")]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IssueCreditNote(
        [FromBody] IssueCreditNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _creditNoteHandler.HandleAsync(
            new IssueCreditNoteCommand(request.OriginalFiscalDocumentId, request.SaleId, request.Amount),
            cancellationToken);
        var body = ApiResponse<FiscalDocumentResponse>.FromResult(result);
        return result.IsSuccess ? Ok(body) : BadRequest(body);
    }
}
