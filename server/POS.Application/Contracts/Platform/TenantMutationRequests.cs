namespace POS.Application.Contracts.Platform;

public sealed class CreateTenantApiRequest
{
    public required string Name { get; init; }

    public string? ContactEmail { get; init; }

    public string BusinessType { get; init; } = "Kiosco";

    public required string AdminEmail { get; init; }

    public string? AdminFullName { get; init; }

    public required string AdminPassword { get; init; }

    /// <summary>Preset opcional: Starter, Pro, Unlimited. Los campos de caps lo pueden sobrescribir.</summary>
    public string? PlanCode { get; init; }

    public int? MaxProducts { get; init; }

    public int? MaxTenantUsers { get; init; }

    public bool? SalesEnabled { get; init; }
}

public sealed class UpdateTenantApiRequest
{
    public required string Name { get; init; }

    public string? ContactEmail { get; init; }
}
