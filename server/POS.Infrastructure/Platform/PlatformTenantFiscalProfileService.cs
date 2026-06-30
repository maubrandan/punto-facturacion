using FluentValidation;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Fiscal;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class PlatformTenantFiscalProfileService : IPlatformTenantFiscalProfileService
{
    private readonly ApplicationDbContext _db;
    private readonly IPlatformDirectoryQuery _tenants;
    private readonly IPlatformAuditService _audit;
    private readonly IValidator<UpsertPlatformTenantFiscalProfileCommand> _validator;

    public PlatformTenantFiscalProfileService(
        ApplicationDbContext db,
        IPlatformDirectoryQuery tenants,
        IPlatformAuditService audit,
        IValidator<UpsertPlatformTenantFiscalProfileCommand> validator)
    {
        _db = db;
        _tenants = tenants;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<TenantFiscalProfileResponse>> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var id = tenantId.Trim();
        if (await _tenants.GetTenantByIdAsync(id, cancellationToken) is null)
            return Result<TenantFiscalProfileResponse>.Failure("tenant.not_found", "No existe el tenant.");

        var profile = await _db.TenantFiscalProfiles
            .FilterByTenant(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return Result<TenantFiscalProfileResponse>.Failure(
                "fiscal.profile_not_found",
                "No hay perfil fiscal configurado para este tenant.");
        }

        return Result<TenantFiscalProfileResponse>.Ok(TenantFiscalProfileResponse.FromEntity(profile));
    }

    public async Task<Result<TenantFiscalProfileResponse>> UpsertAsync(
        string tenantId,
        UpsertTenantFiscalProfileRequest values,
        string justification,
        CancellationToken cancellationToken = default)
    {
        var cmd = new UpsertPlatformTenantFiscalProfileCommand(tenantId.Trim(), values, justification.Trim());
        var validation = await _validator.ValidateAsync(cmd, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<TenantFiscalProfileResponse>.Failure(
                "fiscal.profile_validation",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        if (await _tenants.GetTenantByIdAsync(cmd.TenantId, cancellationToken) is null)
            return Result<TenantFiscalProfileResponse>.Failure("tenant.not_found", "No existe el tenant.");

        var now = DateTime.UtcNow;
        var taxId = new string(cmd.Values.TaxId.Where(char.IsDigit).ToArray());
        var profile = await _db.TenantFiscalProfiles
            .FilterByTenant(cmd.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            profile = new TenantFiscalProfile
            {
                Id = Guid.NewGuid(),
                TenantId = cmd.TenantId,
                CreatedAtUtc = now
            };
            _db.TenantFiscalProfiles.Add(profile);
        }

        profile.TaxId = taxId;
        profile.PointOfSale = cmd.Values.PointOfSale;
        profile.IsProduction = cmd.Values.IsProduction;
        profile.IsEnabled = cmd.Values.IsEnabled;
        profile.CertificateRef = cmd.Values.CertificateRef.Trim();
        profile.PrivateKeyRef = cmd.Values.PrivateKeyRef.Trim();
        profile.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                "TenantFiscalProfileUpdatedByPlatform",
                "TenantFiscalProfile",
                cmd.TenantId,
                $"CUIT={taxId}, PV={cmd.Values.PointOfSale}, prod={cmd.Values.IsProduction}",
                cmd.Justification,
                cmd.TenantId),
            cancellationToken);

        return Result<TenantFiscalProfileResponse>.Ok(TenantFiscalProfileResponse.FromEntity(profile));
    }
}
