using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Common;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Email;

namespace POS.Infrastructure.TenantUsers;

public sealed class RequestTenantUserPasswordResetHandler : IRequestTenantUserPasswordResetHandler
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUserService _current;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<RequestTenantUserPasswordResetHandler> _logger;

    public RequestTenantUserPasswordResetHandler(
        UserManager<ApplicationUser> users,
        ICurrentUserService current,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<RequestTenantUserPasswordResetHandler> logger)
    {
        _users = users;
        _current = current;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
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

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Result<object?>.Failure(
                "tenant.users.email_missing",
                "El usuario no tiene email para enviar el restablecimiento.");
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        try
        {
            await _emailSender.SendAsync(
                AuthEmailComposer.PasswordReset(_emailOptions, user.Email, token),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar email de reset a {Email}", user.Email);
            return Result<object?>.Failure(
                "tenant.users.email_send_failed",
                "No se pudo enviar el correo de restablecimiento.");
        }

        _logger.LogInformation("Password reset enviado a {Email}", user.Email);
        return Result<object?>.Ok(null);
    }
}
