namespace POS.Application.Contracts.Platform;

public sealed class PlatformOperatorDirectoryPageDto
{
    public IReadOnlyList<PlatformOperatorSummaryDto> Items { get; init; } = Array.Empty<PlatformOperatorSummaryDto>();

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
