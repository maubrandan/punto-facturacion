namespace POS.Application.Contracts.Platform;

public sealed class ImpersonationSessionResponseDto
{
    public required string AccessToken { get; init; }

    public required string TokenType { get; init; }

    public required int ExpiresIn { get; init; }

    public required string TenantId { get; init; }
}
