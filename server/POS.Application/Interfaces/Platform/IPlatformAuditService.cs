using POS.Application.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformAuditService
{
    Task LogAsync(PlatformAuditEventData data, CancellationToken cancellationToken = default);
}
