using POS.Domain.Entities;

namespace POS.Application.Contracts.Platform;

public sealed class TenantSummaryDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? ContactEmail { get; init; }

    public TenantStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }
}
