using POS.Domain.Entities;

namespace POS.Application.Contracts.Platform;

public sealed class TenantDetailDto
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? ContactEmail { get; init; }

    public required string BusinessType { get; init; }

    public TenantStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public DateTime? SuspendedAt { get; init; }

    public DateTime? ClosedAt { get; init; }
}
