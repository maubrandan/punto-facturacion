using POS.Domain.Entities;

namespace POS.Application.Contracts.Providers;

public sealed class ProviderResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string TaxId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public static ProviderResponse FromEntity(Provider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        TaxId = p.TaxId,
        Email = p.Email,
        Phone = p.Phone
    };
}
