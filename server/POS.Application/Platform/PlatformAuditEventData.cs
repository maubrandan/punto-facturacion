namespace POS.Application.Platform;

/// <summary>Datos mínimos de auditoría de plataforma (Fase 10: persistir en tabla definitiva).</summary>
public sealed record PlatformAuditEventData(
    string Action,
    string? ResourceType,
    string? ResourceId,
    string? Details,
    string? Justification = null,
    string? AffectedTenantId = null);
