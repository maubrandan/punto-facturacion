namespace POS.Application.Contracts.Platform;

/// <summary>Aviso de resultado cuando la mutación no devuelve entidad (p. ej. solo auditoría / pipeline de correo).</summary>
public sealed class PlatformMutationAckDto
{
    public required string Message { get; init; }
}
