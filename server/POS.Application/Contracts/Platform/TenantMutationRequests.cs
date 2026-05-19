namespace POS.Application.Contracts.Platform;

public sealed class CreateTenantApiRequest
{
    public required string Name { get; init; }

    public string? ContactEmail { get; init; }
}

public sealed class UpdateTenantApiRequest
{
    public required string Name { get; init; }

    public string? ContactEmail { get; init; }
}
