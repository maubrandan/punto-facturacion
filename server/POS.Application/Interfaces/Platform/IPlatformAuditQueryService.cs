using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformAuditQueryService
{
    Task<PlatformAuditEventPageDto> GetPageAsync(
        int page,
        int pageSize,
        PlatformAuditListFilter? filter = null,
        CancellationToken cancellationToken = default);
}
