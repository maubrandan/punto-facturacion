using POS.Domain.Entities;

namespace POS.Application.Contracts.Customers;

public sealed class CustomerResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string TaxId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public static CustomerResponse FromEntity(Customer c) =>
        new()
        {
            Id = c.Id,
            Name = c.Name,
            TaxId = c.TaxId,
            Email = c.Email,
            Phone = c.Phone,
            Address = c.Address,
            CreatedAt = c.CreatedAt
        };
}

public sealed class CreateCustomerRequest
{
    public required string Name { get; init; }

    public required string TaxId { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Address { get; init; }
}

public sealed class UpdateCustomerRequest
{
    public required string Name { get; init; }

    public required string TaxId { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Address { get; init; }
}
