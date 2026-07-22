namespace POS.Application.Platform;

/// <summary>Policies ASP.NET Core para API <c>api/platform/*</c> (ADR 0002) y tenant POS.</summary>
public static class AuthorizationPolicies
{
    public const string PlatformUser = "Platform.User";

    public const string PlatformReadOnly = "Platform.ReadOnly";

    public const string PlatformOperations = "Platform.Operations";

    public const string PlatformSuperAdmin = "Platform.SuperAdmin";

    /// <summary>Iniciar sesión de suplantación (roles de soporte u operaciones).</summary>
    public const string PlatformImpersonation = "Platform.Impersonation";

    public const string TenantAdmin = "Tenant.Admin";

    public const string TenantCashierOrAdmin = "Tenant.CashierOrAdmin";

    public const string TenantStockOrAdmin = "Tenant.StockOrAdmin";
}
