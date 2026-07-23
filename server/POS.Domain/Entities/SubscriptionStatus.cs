namespace POS.Domain.Entities;

/// <summary>Estado comercial de la suscripción SaaS del tenant (billing manual / futuro gateway).</summary>
public enum SubscriptionStatus
{
    Trialing = 0,

    Active = 1,

    PastDue = 2,

    Canceled = 3
}
