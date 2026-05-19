using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;

namespace POS.Infrastructure.Platform;

public sealed class ImpersonationSessionService : IImpersonationSessionService
{
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwt;
    private readonly IPlatformDirectoryQuery _tenants;
    private readonly IPlatformAuditService _audit;
    private readonly IValidator<StartImpersonationSessionCommand> _validator;

    public ImpersonationSessionService(
        ICurrentUserService currentUser,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwt,
        IPlatformDirectoryQuery tenants,
        IPlatformAuditService audit,
        IValidator<StartImpersonationSessionCommand> validator)
    {
        _currentUser = currentUser;
        _userManager = userManager;
        _jwt = jwt;
        _tenants = tenants;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<ImpersonationSessionResponseDto>> StartAsync(
        StartImpersonationSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _validator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<ImpersonationSessionResponseDto>.Failure(
                "impersonation.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var operatorId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            return Result<ImpersonationSessionResponseDto>.Failure(
                "impersonation.unauthenticated",
                "No hay operador autenticado.");
        }

        var platformUser = await _userManager.FindByIdAsync(operatorId);
        if (platformUser is null || platformUser.AccountKind != UserAccountKind.PlatformUser)
        {
            return Result<ImpersonationSessionResponseDto>.Failure(
                "impersonation.not_platform_operator",
                "Solo operadores de plataforma pueden iniciar suplantación.");
        }

        var tenant = await _tenants.GetTenantByIdAsync(command.TenantId.Trim(), cancellationToken);
        if (tenant is null)
        {
            return Result<ImpersonationSessionResponseDto>.Failure(
                "tenant.not_found",
                "No existe el tenant.");
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Result<ImpersonationSessionResponseDto>.Failure(
                "impersonation.tenant_not_active",
                "El negocio debe estar activo para suplantación.");
        }

        var token = _jwt.CreateImpersonationToken(
            platformUser,
            tenant.Id,
            command.Reason.Trim(),
            command.TtlMinutes,
            cancellationToken);

        await _audit.LogAsync(
            new PlatformAuditEventData(
                "ImpersonationSessionStarted",
                nameof(Tenant),
                tenant.Id,
                $"ttlMinutes={command.TtlMinutes};operator={operatorId}",
                command.Reason.Trim(),
                tenant.Id),
            cancellationToken);

        return Result<ImpersonationSessionResponseDto>.Ok(
            new ImpersonationSessionResponseDto
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = command.TtlMinutes * 60,
                TenantId = tenant.Id
            });
    }
}
