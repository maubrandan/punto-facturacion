using System.Linq;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class PlatformTenantLifecycleService : IPlatformTenantLifecycleService
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<CreatePlatformTenantCommand> _createValidator;
    private readonly IValidator<UpdatePlatformTenantCommand> _updateValidator;
    private readonly IValidator<SuspendPlatformTenantCommand> _suspendValidator;
    private readonly IValidator<ClosePlatformTenantCommand> _closeValidator;

    public PlatformTenantLifecycleService(
        ApplicationDbContext db,
        IValidator<CreatePlatformTenantCommand> createValidator,
        IValidator<UpdatePlatformTenantCommand> updateValidator,
        IValidator<SuspendPlatformTenantCommand> suspendValidator,
        IValidator<ClosePlatformTenantCommand> closeValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _suspendValidator = suspendValidator;
        _closeValidator = closeValidator;
    }

    public async Task<Result<TenantDetailDto>> CreateAsync(
        CreatePlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var entity = new Tenant
        {
            Id = id,
            Name = command.Name.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(command.ContactEmail)
                ? null
                : command.ContactEmail.Trim(),
            Status = TenantStatus.Active,
            CreatedAt = now
        };

        _db.Tenants.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }

    public async Task<Result<TenantDetailDto>> UpdateAsync(
        UpdatePlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken);
        if (entity is null)
            return Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (!TenantLifecycleRules.CanUpdate(entity.Status))
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.closed",
                "No se puede editar un negocio cerrado.");
        }

        entity.Name = command.Name.Trim();
        entity.ContactEmail = string.IsNullOrWhiteSpace(command.ContactEmail)
            ? null
            : command.ContactEmail.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }

    public async Task<Result<TenantDetailDto>> SuspendAsync(
        SuspendPlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _suspendValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken);
        if (entity is null)
            return Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (entity.Status == TenantStatus.Suspended)
            return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));

        if (entity.Status == TenantStatus.Closed)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.invalid",
                "No se puede suspender un negocio cerrado.");
        }

        if (!TenantLifecycleRules.CanSuspend(entity.Status))
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.invalid",
                "Transición de estado no válida.");
        }

        var now = DateTime.UtcNow;
        entity.Status = TenantStatus.Suspended;
        entity.SuspendedAt = now;
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }

    public async Task<Result<TenantDetailDto>> CloseAsync(
        ClosePlatformTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _closeValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == command.TenantId, cancellationToken);
        if (entity is null)
            return Result<TenantDetailDto>.Failure("tenant.not_found", "No existe el tenant.");

        if (entity.Status == TenantStatus.Closed)
            return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));

        if (!TenantLifecycleRules.CanClose(entity.Status))
        {
            return Result<TenantDetailDto>.Failure(
                "tenant.lifecycle.invalid",
                "No se puede cerrar desde el estado actual.");
        }

        var now = DateTime.UtcNow;
        entity.Status = TenantStatus.Closed;
        entity.ClosedAt = now;
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<TenantDetailDto>.Ok(TenantMappings.ToDetailDto(entity));
    }
}
