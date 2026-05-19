namespace POS.Application.Contracts.Platform;

public sealed class PlatformAuditEventDto
{
    public long Id { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? ActorUserId { get; init; }

    public string? ActorEmail { get; init; }

    public string? ResourceType { get; init; }

    public string? ResourceId { get; init; }

    public string? AffectedTenantId { get; init; }

    public string? Details { get; init; }

    public string? Justification { get; init; }

    public string? CorrelationId { get; init; }

    public string? IpAddress { get; init; }

    public bool IsImpersonationContext { get; init; }
}

public sealed class PlatformAuditListFilter
{
    public string? TenantId { get; init; }

    public string? ActorUserId { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }
}

public sealed class PlatformAuditEventPageDto
{
    public IReadOnlyList<PlatformAuditEventDto> Items { get; init; } = Array.Empty<PlatformAuditEventDto>();

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
