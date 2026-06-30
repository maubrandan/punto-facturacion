using POS.Application.Contracts.Fiscal;

namespace POS.Application.Interfaces;

public interface IFiscalQueryService
{
    Task<IReadOnlyList<FiscalDocumentResponse>> GetBySaleIdAsync(
        Guid saleId,
        CancellationToken cancellationToken = default);

    Task<FiscalDocumentResponse?> GetByIdAsync(
        Guid fiscalDocumentId,
        CancellationToken cancellationToken = default);
}
