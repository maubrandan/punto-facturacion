namespace POS.Application.Contracts.Platform;

public sealed class TenantUserSummaryDto
{
    public required string Id { get; init; }

    public required string Email { get; init; }

    public string FullName { get; init; } = string.Empty;

    public bool EmailConfirmed { get; init; }

    public bool LockoutEnabled { get; init; }

    public DateTimeOffset? LockoutEnd { get; init; }

    public bool BlockedByPlatform { get; init; }
}
