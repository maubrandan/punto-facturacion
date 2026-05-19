namespace POS.Application.Contracts.Auth;

public sealed class RegisterRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string BusinessName { get; init; }

    public string? FullName { get; init; }

    public string? BusinessType { get; init; }
}
