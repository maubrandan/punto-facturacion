using System.Linq;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class PlatformTenantEntitlementsService : IPlatformTenantEntitlementsService
{
    private readonly ApplicationDbContext _db;
    private readonly IPlatformDirectoryQuery _tenants;
    private readonly IPlatformAuditService _audit;
    private readonly IValidator<SetTenantEntitlementsCommand> _validator;

    public PlatformTenantEntitlementsService(
        ApplicationDbContext db,
        IPlatformDirectoryQuery tenants,
        IPlatformAuditService audit,
        IValidator<SetTenantEntitlementsCommand> validator)
    {
        _db = db;
        _tenants = tenants;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<TenantEntitlementsDto>> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (await _tenants.GetTenantByIdAsync(tenantId.Trim(), cancellationToken) is null)
            return Result<TenantEntitlementsDto>.Failure("tenant.not_found", "No existe el tenant.");

        var row = await _db.Set<TenantEntitlement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId.Trim(), cancellationToken);
        return Result<TenantEntitlementsDto>.Ok(TenantEntitlementsMapper.FromRow(row));
    }

    public async Task<Result<TenantEntitlementsDto>> SetAsync(
        string tenantId,
        TenantEntitlementsDto values,
        string justification,
        CancellationToken cancellationToken = default)
    {
        var cmd = new SetTenantEntitlementsCommand(
            tenantId.Trim(),
            values.MaxProducts,
            values.MaxTenantUsers,
            values.SalesEnabled,
            justification);

        var v = await _validator.ValidateAsync(cmd, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantEntitlementsDto>.Failure(
                "tenant_entitlements.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        if (await _tenants.GetTenantByIdAsync(cmd.TenantId, cancellationToken) is null)
            return Result<TenantEntitlementsDto>.Failure("tenant.not_found", "No existe el tenant.");

        var justificationTrim = justification.Trim();
        var now = DateTime.UtcNow;
        var caps = new TenantEntitlementsDto
        {
            MaxProducts = cmd.MaxProducts,
            MaxTenantUsers = cmd.MaxTenantUsers,
            SalesEnabled = cmd.SalesEnabled
        };
        var existing = await _db.Set<TenantEntitlement>()
            .FirstOrDefaultAsync(e => e.TenantId == cmd.TenantId, cancellationToken);
        if (existing is null)
            _db.Set<TenantEntitlement>().Add(TenantEntitlementsMapper.ToNewRow(cmd.TenantId, caps, now));
        else
            TenantEntitlementsMapper.Apply(existing, caps, now);

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                "TenantEntitlementsUpdated",
                nameof(TenantEntitlement),
                cmd.TenantId,
                $"maxProducts={Describe(cmd.MaxProducts)};maxTenantUsers={Describe(cmd.MaxTenantUsers)};salesEnabled={cmd.SalesEnabled}",
                justificationTrim,
                cmd.TenantId),
            cancellationToken);

        return await GetAsync(cmd.TenantId, cancellationToken);
    }

    private static string Describe(int? n) => n.HasValue ? n.Value.ToString() : "(sin límite)";
}
