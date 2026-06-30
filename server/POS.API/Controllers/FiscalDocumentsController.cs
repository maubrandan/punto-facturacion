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
    private readonly IFiscalQueryService _queryService;

    public FiscalDocumentsController(
        IIssueElectronicInvoiceHandler issueHandler,
        IRetryElectronicInvoiceHandler retryHandler,
        IIssueCreditNoteHandler creditNoteHandler,
        IFiscalQueryService queryService)
    {
        _issueHandler = issueHandler;
        _retryHandler = retryHandler;
        _creditNoteHandler = creditNoteHandler;
        _queryService = queryService;
    }

    [HttpGet("by-sale/{saleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FiscalDocumentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySale(Guid saleId, CancellationToken cancellationToken)
    {
        var documents = await _queryService.GetBySaleIdAsync(saleId, cancellationToken);
        return Ok(
            ApiResponse<IReadOnlyList<FiscalDocumentResponse>>.FromResult(
                Result<IReadOnlyList<FiscalDocumentResponse>>.Ok(documents)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var document = await _queryService.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return NotFound(
                ApiResponse<FiscalDocumentResponse>.FromResult(
                    Result<FiscalDocumentResponse>.Failure("fiscal.not_found", "Comprobante fiscal no encontrado.")));
        }

        return Ok(
            ApiResponse<FiscalDocumentResponse>.FromResult(
                Result<FiscalDocumentResponse>.Ok(document)));
    }

    [HttpPost("issue")]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FiscalDocumentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Issue(
        [FromBody] IssueElectronicInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _issueHandler.HandleAsync(
            new IssueElectronicInvoiceCommand(
                request.SaleId,
                request.IsInvoiceA,
                request.BuyerTaxId,
                request.BuyerName),
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
