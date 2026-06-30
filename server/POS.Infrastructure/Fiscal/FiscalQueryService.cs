using Microsoft.EntityFrameworkCore;
using POS.Application.Contracts.Fiscal;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Fiscal;

public sealed class FiscalQueryService : IFiscalQueryService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FiscalQueryService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<FiscalDocumentResponse>> GetBySaleIdAsync(
        Guid saleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var profileTaxId = await GetProfileTaxIdAsync(tenantId, cancellationToken);

        var documents = await _db.Set<FiscalDocument>()
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.SaleId == saleId)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return documents
            .Select(d => FiscalDocumentMapper.ToResponse(d, profileTaxId))
            .ToList();
    }

    public async Task<FiscalDocumentResponse?> GetByIdAsync(
        Guid fiscalDocumentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var document = await _db.Set<FiscalDocument>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == fiscalDocumentId && d.TenantId == tenantId, cancellationToken);
        if (document is null)
            return null;

        var profileTaxId = await GetProfileTaxIdAsync(tenantId, cancellationToken);
        return FiscalDocumentMapper.ToResponse(document, profileTaxId);
    }

    private string RequireTenantId()
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new InvalidOperationException("No se pudo resolver el tenant actual.");
        return tenantId;
    }

    private async Task<string?> GetProfileTaxIdAsync(string tenantId, CancellationToken cancellationToken) =>
        await _db.Set<TenantFiscalProfile>()
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.TaxId)
            .FirstOrDefaultAsync(cancellationToken);
}
