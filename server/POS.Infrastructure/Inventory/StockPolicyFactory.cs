using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Application.Inventory;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Inventory;

public sealed class StockPolicyFactory : IStockPolicyFactory
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PharmacyStockPolicy _pharmacy;
    private readonly HardwareStockPolicy _hardware;
    private readonly KioskStockPolicy _kiosk;

    public StockPolicyFactory(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        PharmacyStockPolicy pharmacy,
        HardwareStockPolicy hardware,
        KioskStockPolicy kiosk)
    {
        _db = db;
        _currentUser = currentUser;
        _pharmacy = pharmacy;
        _hardware = hardware;
        _kiosk = kiosk;
    }

    public async Task<IStockPolicy> ForCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException("No hay TenantId en el contexto actual.");

        var businessType = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.BusinessType)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(businessType))
        {
            // Ventana de migración: fallback a claim/usuario.
            businessType = BusinessTypeNames.Kiosco;
        }

        return await ForBusinessTypeAsync(businessType, cancellationToken);
    }

    public Task<IStockPolicy> ForBusinessTypeAsync(string businessType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = BusinessTypeNames.IsKnown(businessType)
            ? BusinessTypeNames.Normalize(businessType)
            : BusinessTypeNames.Kiosco;

        IStockPolicy policy = normalized switch
        {
            BusinessTypeNames.Farmacia => _pharmacy,
            BusinessTypeNames.Ferreteria => _hardware,
            _ => _kiosk
        };

        return Task.FromResult(policy);
    }
}
