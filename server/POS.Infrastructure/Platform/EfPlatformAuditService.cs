using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class EfPlatformAuditService : IPlatformAuditService
{
    private const string CorrelationHeaderName = "X-Request-Id";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EfPlatformAuditService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(PlatformAuditEventData data, CancellationToken cancellationToken = default)
    {
        var http = _httpContextAccessor.HttpContext;
        var actorEmail = http?.User.FindFirstValue(ClaimTypes.Email) ?? http?.User.FindFirstValue("email");
        var correlationId = http?.Response.Headers[CorrelationHeaderName].FirstOrDefault()
            ?? http?.Request.Headers[CorrelationHeaderName].FirstOrDefault();
        var ip = http?.Connection.RemoteIpAddress?.ToString();

        var row = new PlatformAuditEvent
        {
            CreatedAtUtc = DateTime.UtcNow,
            Action = data.Action.Trim(),
            ActorUserId = _currentUser.UserId?.Trim(),
            ActorEmail = actorEmail?.Trim(),
            ResourceType = data.ResourceType?.Trim(),
            ResourceId = data.ResourceId?.Trim(),
            AffectedTenantId = data.AffectedTenantId?.Trim(),
            Details = data.Details?.Trim(),
            Justification = data.Justification?.Trim(),
            CorrelationId = correlationId?.Trim(),
            IpAddress = ip?.Trim(),
            IsImpersonationContext = _currentUser.IsImpersonationContext
        };

        _db.PlatformAuditEvents.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
