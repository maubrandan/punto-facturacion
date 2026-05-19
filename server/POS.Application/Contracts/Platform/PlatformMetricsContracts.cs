namespace POS.Application.Contracts.Platform;

public sealed class PlatformMetricsOverviewDto
{
    public int TotalTenants { get; init; }

    public int ActiveTenants { get; init; }

    public int SuspendedTenants { get; init; }

    public int ClosedTenants { get; init; }

    public int BlockedTenantUsers { get; init; }

    public int RecentAuditEvents { get; init; }
}
