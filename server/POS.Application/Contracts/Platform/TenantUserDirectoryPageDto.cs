namespace POS.Application.Contracts.Platform;

public sealed class TenantUserDirectoryPageDto
{
    public IReadOnlyList<TenantUserSummaryDto> Items { get; init; } = Array.Empty<TenantUserSummaryDto>();

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
