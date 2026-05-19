using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using POS.Application.Interfaces;
using POS.Application.Platform;

namespace POS.Infrastructure.Services;

/// <summary>
/// Resuelve tenant y usuario desde el <see cref="HttpContext"/> autenticado (claims)
/// o desde <see cref="ICurrentUserTenantContext"/> (p. ej. registro sin JWT).
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    public const string TenantIdClaimType = "tenant_id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserTenantContext _tenantContext;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserTenantContext tenantContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? TenantId
    {
        get
        {
            var t = _tenantContext.OverriddenTenantId;
            if (!string.IsNullOrWhiteSpace(t))
                return t.Trim();
            return User?.FindFirstValue(TenantIdClaimType);
        }
    }

    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub");

    public bool IsPlatformContext =>
        string.Equals(
            User?.FindFirstValue(PlatformClaimTypes.IsPlatform),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public bool IsImpersonationContext =>
        string.Equals(
            User?.FindFirstValue(PlatformClaimTypes.Impersonation),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
