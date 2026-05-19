namespace POS.Application.Platform;

/// <summary>Instrucción para aprovisionar un operador de plataforma (Identity + rol) sin flujo HTTP.</summary>
public sealed record ProvisionPlatformUserCommand(
    string Email,
    string Password,
    string FullName,
    string PlatformRole
);
