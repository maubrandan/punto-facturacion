namespace POS.Application.Contracts.Platform;

public sealed class TenantDirectoryPageDto
{
    public IReadOnlyList<TenantSummaryDto> Items { get; init; } = Array.Empty<TenantSummaryDto>();

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
