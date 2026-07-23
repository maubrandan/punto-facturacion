using POS.Application.Common;
using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces;

/// <summary>Lectura de suscripción del tenant del JWT actual (Admin).</summary>
public interface ITenantSubscriptionQuery
{
    Task<Result<TenantSubscriptionDto>> GetForCurrentTenantAsync(CancellationToken cancellationToken = default);
}
