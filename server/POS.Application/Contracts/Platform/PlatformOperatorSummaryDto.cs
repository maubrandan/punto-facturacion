namespace POS.Application.Contracts.Platform;

public sealed class PlatformOperatorSummaryDto
{
    public required string Id { get; init; }

    public required string Email { get; init; }

    public string FullName { get; init; } = string.Empty;

    public required string PlatformRole { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool BlockedByPlatform { get; init; }
}
