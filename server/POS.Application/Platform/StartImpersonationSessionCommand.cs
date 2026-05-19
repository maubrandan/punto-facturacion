namespace POS.Application.Platform;

public sealed record StartImpersonationSessionCommand(string TenantId, string Reason, int TtlMinutes);
