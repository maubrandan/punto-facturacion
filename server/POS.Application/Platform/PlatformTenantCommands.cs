namespace POS.Application.Platform;

public sealed record CreatePlatformTenantCommand(
    string Name,
    string? ContactEmail,
    string BusinessType,
    string AdminEmail,
    string? AdminFullName,
    string AdminPassword,
    string? PlanCode = null,
    int? MaxProducts = null,
    int? MaxTenantUsers = null,
    bool? SalesEnabled = null);

public sealed record UpdatePlatformTenantCommand(string TenantId, string Name, string? ContactEmail);

public sealed record SuspendPlatformTenantCommand(string TenantId);

public sealed record UnsuspendPlatformTenantCommand(string TenantId);

public sealed record ClosePlatformTenantCommand(string TenantId);

public sealed record ReopenPlatformTenantCommand(string TenantId, string Justification);
