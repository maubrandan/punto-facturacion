namespace POS.Application.Contracts.Platform;

/// <summary>Justificación obligatoria para mutaciones sensibles desde consola de plataforma (Fase 6).</summary>
public sealed class PlatformUserActionRequest
{
    public required string Justification { get; init; }
}
