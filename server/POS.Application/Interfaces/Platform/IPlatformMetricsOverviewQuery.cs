using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformMetricsOverviewQuery
{
    Task<PlatformMetricsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
