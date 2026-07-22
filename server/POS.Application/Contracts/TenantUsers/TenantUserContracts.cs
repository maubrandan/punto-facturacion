namespace POS.Application.Contracts.TenantUsers;

public sealed class TenantUserListItemDto
{
    public required string Id { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public required string Role { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool BlockedByTenant { get; init; }

    public bool BlockedByPlatform { get; init; }

    public bool LockoutEnabled { get; init; }

    public DateTimeOffset? LockoutEnd { get; init; }
}

public sealed class TenantUserDirectoryPageDto
{
    public IReadOnlyList<TenantUserListItemDto> Items { get; init; } = Array.Empty<TenantUserListItemDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class CreateTenantUserApiRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public string FullName { get; init; } = string.Empty;

    public required string Role { get; init; }
}

public sealed class UpdateTenantUserApiRequest
{
    public required string FullName { get; init; }

    public required string Role { get; init; }
}
