namespace POS.Application.Contracts.Platform;

public sealed class PlatformHealthResponse
{
    public required string Status { get; init; }

    public required string Service { get; init; }
}

public sealed class PlatformVersionResponse
{
    public required string Version { get; init; }

    public required string AssemblyName { get; init; }
}
