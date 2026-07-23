namespace POS.Application.Platform;

public sealed record UpdatePlatformOperatorCommand(string UserId, string FullName, string PlatformRole);

public sealed record BlockPlatformOperatorCommand(string UserId, string Justification);

public sealed record UnblockPlatformOperatorCommand(string UserId, string Justification);
