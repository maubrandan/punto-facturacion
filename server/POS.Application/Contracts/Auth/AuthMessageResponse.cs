namespace POS.Application.Contracts.Auth;

/// <summary>Respuesta breve de acciones públicas de auth (confirmación / reset).</summary>
public sealed class AuthMessageResponse
{
    public required string Message { get; init; }
}
