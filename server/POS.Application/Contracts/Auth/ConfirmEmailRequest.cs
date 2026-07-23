namespace POS.Application.Contracts.Auth;

public sealed class ConfirmEmailRequest
{
    public required string Email { get; init; }

    public required string Token { get; init; }
}
