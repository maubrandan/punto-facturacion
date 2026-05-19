namespace POS.Application.Contracts.Providers;

public sealed class CreateProviderRequest
{
    public required string Name { get; init; }

    public required string TaxId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;
}
