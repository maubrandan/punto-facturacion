using POS.Application.Interfaces.Platform;
using POS.Application.Platform;

namespace POS.Infrastructure.Platform;

public sealed class NoOpPlatformAuditService : IPlatformAuditService
{
    public Task LogAsync(PlatformAuditEventData data, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
