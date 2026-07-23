namespace POS.Domain.Entities;

/// <summary>Reglas de alerta de vencimiento (solo significativas para Farmacia).</summary>
public static class PharmacyExpiryAlertRules
{
    public const int DefaultWithinDays = 30;
    public const int MinWithinDays = 1;
    public const int MaxWithinDays = 365;
}

/// <summary>Estados de alerta de lote (API/UI).</summary>
public static class ExpiryAlertStatuses
{
    public const string Expired = "Expired";
    public const string ExpiringSoon = "ExpiringSoon";
}
