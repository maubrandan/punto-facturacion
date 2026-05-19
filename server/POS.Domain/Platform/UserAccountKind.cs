namespace POS.Domain.Platform;

/// <summary>
/// Tipo de cuenta: operador de un negocio (POS) o operador global de plataforma (ver ADR 0001).
/// </summary>
public enum UserAccountKind
{
    TenantUser = 0,
    PlatformUser = 1
}
