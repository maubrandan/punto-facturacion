using POS.Domain.Entities;

namespace POS.Application.Contracts.Platform;

/// <summary>Filtros opcionales para el directorio de tenants (plataforma).</summary>
public sealed class TenantListFilter
{
    public string? NameContains { get; init; }

    public TenantStatus? Status { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }
}
