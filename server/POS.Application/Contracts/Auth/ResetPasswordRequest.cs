namespace POS.Application.Contracts.Auth;

public sealed class ResetPasswordRequest
{
    public required string Email { get; init; }

    public required string Token { get; init; }

    public required string NewPassword { get; init; }
}
