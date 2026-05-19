using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class CurrentUserTenantContext : ICurrentUserTenantContext
{
    public string? OverriddenTenantId { get; set; }
}
