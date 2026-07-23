using POS.Domain.Entities;

namespace POS.Application.Contracts.Platform;

public sealed class SubscriptionInvoiceDto
{
    public required Guid Id { get; init; }

    public required string TenantId { get; init; }

    public required string InvoiceNumber { get; init; }

    public SubscriptionInvoiceStatus Status { get; init; }

    public required string PlanCode { get; init; }

    public BillingCycle BillingCycle { get; init; }

    public DateTime PeriodStartUtc { get; init; }

    public DateTime PeriodEndUtc { get; init; }

    public decimal Amount { get; init; }

    public required string Currency { get; init; }

    public BillingProvider Provider { get; init; }

    public string? ExternalInvoiceId { get; init; }

    public DateTime DueAtUtc { get; init; }

    public DateTime? PaidAtUtc { get; init; }

    public string? Notes { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public sealed class SubscriptionInvoiceListDto
{
    public required IReadOnlyList<SubscriptionInvoiceDto> Items { get; init; }

    public int TotalCount { get; init; }
}
