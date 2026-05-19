using POS.Application.Common;
using POS.Application.Contracts.Fiscal;
using POS.Application.Fiscal;

namespace POS.Application.Interfaces;

public interface IIssueElectronicInvoiceHandler
{
    Task<Result<FiscalDocumentResponse>> HandleAsync(
        IssueElectronicInvoiceCommand command,
        CancellationToken cancellationToken = default);
}

public interface IRetryElectronicInvoiceHandler
{
    Task<Result<FiscalDocumentResponse>> HandleAsync(
        RetryElectronicInvoiceCommand command,
        CancellationToken cancellationToken = default);
}

public interface IIssueCreditNoteHandler
{
    Task<Result<FiscalDocumentResponse>> HandleAsync(
        IssueCreditNoteCommand command,
        CancellationToken cancellationToken = default);
}
