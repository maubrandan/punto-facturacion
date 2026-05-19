using POS.Application.Contracts.Platform;
using POS.Domain.Entities;

namespace POS.Infrastructure.Platform;

internal static class TenantMappings
{
    public static TenantDetailDto ToDetailDto(Tenant t) =>
        new()
        {
            Id = t.Id,
            Name = t.Name,
            ContactEmail = t.ContactEmail,
            Status = t.Status,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            SuspendedAt = t.SuspendedAt,
            ClosedAt = t.ClosedAt
        };
}
