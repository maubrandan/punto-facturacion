namespace POS.Domain.Entities;

/// <summary>
/// Evento de auditoría de operaciones de plataforma (append-only).
/// </summary>
public sealed class PlatformAuditEvent
{
    public long Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? ActorUserId { get; set; }

    public string? ActorEmail { get; set; }

    public string? ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public string? AffectedTenantId { get; set; }

    public string? Details { get; set; }

    public string? Justification { get; set; }

    public string? CorrelationId { get; set; }

    public string? IpAddress { get; set; }

    public bool IsImpersonationContext { get; set; }
}
