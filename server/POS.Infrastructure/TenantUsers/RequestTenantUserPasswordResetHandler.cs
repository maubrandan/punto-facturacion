using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.Common;
using POS.Application.Interfaces;
using POS.Domain.Entities;

namespace POS.Infrastructure.TenantUsers;

public sealed class RequestTenantUserPasswordResetHandler : IRequestTenantUserPasswordResetHandler
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUserService _current;
    private readonly ILogger<RequestTenantUserPasswordResetHandler> _logger;

    public RequestTenantUserPasswordResetHandler(
        UserManager<ApplicationUser> users,
        ICurrentUserService current,
        ILogger<RequestTenantUserPasswordResetHandler> logger)
    {
        _users = users;
        _current = current;
        _logger = logger;
    }

    public async Task<Result<object?>> HandleAsync(string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _current.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<object?>.Failure(
                "tenant.users.tenant_required",
                "No se pudo determinar el negocio.");
        }

        var user = await TenantUserMappings.TenantUsersOf(_users, tenantId)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return Result<object?>.Failure("tenant.users.not_found", "Usuario no encontrado.");

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        _logger.LogInformation(
            "Password reset solicitado para {Email}; token generado (mailer no conectado). Len={Len}",
            user.Email,
            token?.Length ?? 0);

        return Result<object?>.Ok(null);
    }
}
