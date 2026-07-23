namespace POS.Application.Contracts.Auth;

public sealed class ForgotPasswordRequest
{
    public required string Email { get; init; }
}
