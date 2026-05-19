using Microsoft.AspNetCore.Identity;
using POS.Domain.Common;
using POS.Domain.Platform;

namespace POS.Domain.Entities;

public class ApplicationUser : IdentityUser, ITenantEntity
{
    /// <summary>
    /// Negocio al que pertenece. Para <see cref="UserAccountKind.PlatformUser"/> se usa
    /// <see cref="PlatformScope.ReservedTenantId"/> (sentinela, ADR 0001), no un tenant de catálogo.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string BusinessType { get; set; } = "Kiosco";

    public UserAccountKind AccountKind { get; set; } = UserAccountKind.TenantUser;

    /// <summary>
    /// Bloqueo impuesto por operadores de plataforma (independiente del lockout por intentos de Identity).
    /// </summary>
    public bool BlockedByPlatform { get; set; }
}
