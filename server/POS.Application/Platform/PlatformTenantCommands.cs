namespace POS.Application.Platform;

public sealed record CreatePlatformTenantCommand(string Name, string? ContactEmail);

public sealed record UpdatePlatformTenantCommand(string TenantId, string Name, string? ContactEmail);

public sealed record SuspendPlatformTenantCommand(string TenantId);

public sealed record ClosePlatformTenantCommand(string TenantId);
