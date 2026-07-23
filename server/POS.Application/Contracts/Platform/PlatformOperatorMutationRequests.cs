namespace POS.Application.Contracts.Platform;

public sealed class ProvisionPlatformOperatorApiRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string FullName { get; init; }

    public required string PlatformRole { get; init; }
}

public sealed class UpdatePlatformOperatorApiRequest
{
    public required string FullName { get; init; }

    public required string PlatformRole { get; init; }
}
