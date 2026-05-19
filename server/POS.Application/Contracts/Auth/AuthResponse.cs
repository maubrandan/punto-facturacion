namespace POS.Application.Contracts.Auth;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public required string TokenType { get; init; }

    public required int ExpiresIn { get; init; }

    public required string UserId { get; init; }

    public required string Email { get; init; }

    public required string TenantId { get; init; }

    public required string BusinessType { get; init; }
}
