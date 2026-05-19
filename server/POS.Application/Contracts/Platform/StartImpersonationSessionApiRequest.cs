namespace POS.Application.Contracts.Platform;

public sealed class StartImpersonationSessionApiRequest
{
    public required string TenantId { get; init; }

    public required string Reason { get; init; }

    public int TtlMinutes { get; init; } = 15;
}
