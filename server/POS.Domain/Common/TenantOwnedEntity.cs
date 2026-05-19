namespace POS.Domain.Common;

/// <summary>
/// Base opcional para entidades persistidas con <see cref="ITenantEntity.TenantId"/>.
/// </summary>
public abstract class TenantOwnedEntity : ITenantEntity
{
    public string TenantId { get; set; } = string.Empty;
}
