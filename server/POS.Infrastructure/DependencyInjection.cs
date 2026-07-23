using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Interfaces.Billing;
using POS.Application.Billing;
using POS.Application.Billing.Validation;
using POS.Application.Platform;
using POS.Application.Platform.Validation;
using POS.Application.Fiscal;
using POS.Application.Fiscal.Validation;
using POS.Infrastructure.Billing;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Email;
using POS.Infrastructure.Entitlements;
using POS.Infrastructure.Platform;
using POS.Infrastructure.Cash;
using POS.Infrastructure.Customers;
using POS.Infrastructure.Fiscal;
using POS.Infrastructure.Inventory;
using POS.Infrastructure.Purchases;
using POS.Infrastructure.Sales;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Services;
using POS.Infrastructure.TenantUsers;
using POS.Application.TenantUsers;
using POS.Application.TenantUsers.Validation;
using POS.Application.Inventory;
using POS.Application.Inventory.Validation;

namespace POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.Configure<PlatformAdminSeedOptions>(configuration.GetSection(PlatformAdminSeedOptions.SectionName));
        services.Configure<ArcaOptions>(configuration.GetSection(ArcaOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        services.AddScoped<IEmailSender>(EmailSenderRegistration.Create);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserTenantContext, CurrentUserTenantContext>();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());

        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' no está configurada en appsettings.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ICreateSaleHandler, CreateSaleHandler>();
        services.AddScoped<ICreatePurchaseHandler, CreatePurchaseHandler>();
        services.AddScoped<PharmacyStockPolicy>();
        services.AddScoped<HardwareStockPolicy>();
        services.AddScoped<KioskStockPolicy>();
        services.AddScoped<IStockPolicyFactory, StockPolicyFactory>();
        services.AddScoped<IAdjustStockHandler, AdjustStockHandler>();
        services.AddScoped<IInventoryQueryService, InventoryQueryService>();
        services.AddScoped<IValidator<AdjustStockCommand>, AdjustStockCommandValidator>();

        services.AddScoped<IValidator<CreateTenantUserCommand>, CreateTenantUserCommandValidator>();
        services.AddScoped<IValidator<UpdateTenantUserCommand>, UpdateTenantUserCommandValidator>();
        services.AddScoped<ICreateTenantUserHandler, CreateTenantUserHandler>();
        services.AddScoped<IUpdateTenantUserHandler, UpdateTenantUserHandler>();
        services.AddScoped<ISetTenantUserBlockedHandler, SetTenantUserBlockedHandler>();
        services.AddScoped<IListTenantUsersQuery, ListTenantUsersQuery>();
        services.AddScoped<IRequestTenantUserPasswordResetHandler, RequestTenantUserPasswordResetHandler>();

        services.AddSingleton<Fiscal.Afip.AfipWsaaClient>();
        services.AddSingleton<Fiscal.Afip.AfipWsfeClient>();
        services.AddSingleton<Fiscal.Afip.DirectAfipAuthorizationService>();
        services.AddScoped<IFiscalAuthorizationService, ArcaFiscalAuthorizationService>();
        services.AddScoped<IIssueElectronicInvoiceHandler, IssueElectronicInvoiceHandler>();
        services.AddScoped<IRetryElectronicInvoiceHandler, RetryElectronicInvoiceHandler>();
        services.AddScoped<IIssueCreditNoteHandler, IssueCreditNoteHandler>();
        services.AddScoped<IFiscalQueryService, FiscalQueryService>();
        services.AddScoped<IValidator<IssueElectronicInvoiceCommand>, IssueElectronicInvoiceCommandValidator>();
        services.AddScoped<IValidator<RetryElectronicInvoiceCommand>, RetryElectronicInvoiceCommandValidator>();
        services.AddScoped<IValidator<IssueCreditNoteCommand>, IssueCreditNoteCommandValidator>();
        services.AddScoped<ICashSessionService, CashSessionService>();
        services.AddScoped<ICustomerAccountQueryService, CustomerAccountQueryService>();
        services.AddScoped<IRegisterCustomerAccountPaymentHandler, RegisterCustomerAccountPaymentHandler>();
        services.AddScoped<ISalesQueryService, SalesQueryService>();
        services.AddHostedService<FiscalRetryWorker>();

        services.AddScoped<IValidator<ProvisionPlatformUserCommand>, ProvisionPlatformUserCommandValidator>();
        services.AddScoped<IProvisionPlatformUserHandler, ProvisionPlatformUserHandler>();
        services.AddScoped<IValidator<UpdatePlatformOperatorCommand>, UpdatePlatformOperatorCommandValidator>();
        services.AddScoped<IValidator<BlockPlatformOperatorCommand>, BlockPlatformOperatorCommandValidator>();
        services.AddScoped<IValidator<UnblockPlatformOperatorCommand>, UnblockPlatformOperatorCommandValidator>();
        services.AddScoped<IPlatformOperatorQuery, PlatformOperatorQuery>();
        services.AddScoped<IPlatformOperatorAdminService, PlatformOperatorAdminService>();
        services.AddScoped<IPlatformAuditService, EfPlatformAuditService>();
        services.AddScoped<IPlatformAuditQueryService, PlatformAuditQueryService>();
        services.AddScoped<IPlatformMetricsOverviewQuery, PlatformMetricsOverviewQuery>();
        services.AddScoped<IPlatformDirectoryQuery, PlatformDirectoryQuery>();

        services.AddScoped<IValidator<CreatePlatformTenantCommand>, CreatePlatformTenantCommandValidator>();
        services.AddScoped<IValidator<UpdatePlatformTenantCommand>, UpdatePlatformTenantCommandValidator>();
        services.AddScoped<IValidator<SuspendPlatformTenantCommand>, SuspendPlatformTenantCommandValidator>();
        services.AddScoped<IValidator<UnsuspendPlatformTenantCommand>, UnsuspendPlatformTenantCommandValidator>();
        services.AddScoped<IValidator<ClosePlatformTenantCommand>, ClosePlatformTenantCommandValidator>();
        services.AddScoped<IValidator<ReopenPlatformTenantCommand>, ReopenPlatformTenantCommandValidator>();
        services.AddScoped<IPlatformTenantLifecycleService, PlatformTenantLifecycleService>();

        services.AddScoped<ITenantEntitlementGuard, TenantEntitlementGuard>();
        services.AddScoped<IValidator<SetTenantEntitlementsCommand>, SetTenantEntitlementsCommandValidator>();
        services.AddScoped<IPlatformTenantEntitlementsService, PlatformTenantEntitlementsService>();
        services.AddScoped<PlatformTenantSubscriptionService>();
        services.AddScoped<IPlatformTenantSubscriptionService>(sp =>
            sp.GetRequiredService<PlatformTenantSubscriptionService>());
        services.AddScoped<IValidator<UpdateTenantSubscriptionCommand>, UpdateTenantSubscriptionCommandValidator>();
        services.AddScoped<ITenantSubscriptionQuery, TenantSubscriptionQuery>();
        services.AddScoped<IValidator<SelfServeUpgradeSubscriptionCommand>, SelfServeUpgradeSubscriptionCommandValidator>();
        services.AddScoped<ISelfServeUpgradeSubscriptionHandler, SelfServeUpgradeSubscriptionHandler>();
        services.AddScoped<ITenantSubscriptionInvoiceQuery, TenantSubscriptionInvoiceQuery>();
        services.AddScoped<ISubscriptionInvoiceFactory, SubscriptionInvoiceFactory>();
        services.AddScoped<ISubscriptionBillingJobs, SubscriptionBillingJobs>();
        services.AddScoped<ManualBillingGateway>();
        services.AddScoped<StripeBillingGateway>();
        services.AddScoped<MercadoPagoBillingGateway>();
        services.AddScoped<IBillingGatewayResolver, BillingGatewayResolver>();
        services.AddScoped<IBillingWebhookProcessor, BillingWebhookProcessor>();
        services.AddHostedService<BillingRenewalWorker>();
        services.AddHostedService<BillingDunningWorker>();
        services.AddScoped<IValidator<UpsertPlatformTenantFiscalProfileCommand>, UpsertPlatformTenantFiscalProfileCommandValidator>();
        services.AddScoped<IPlatformTenantFiscalProfileService, PlatformTenantFiscalProfileService>();

        services.AddScoped<IValidator<PlatformUserActionRequest>, PlatformUserActionRequestValidator>();
        services.AddScoped<IPlatformTenantUserQuery, PlatformTenantUserQuery>();
        services.AddScoped<IPlatformTenantUserAdminService, PlatformTenantUserAdminService>();

        services.AddScoped<IValidator<StartImpersonationSessionCommand>, StartImpersonationSessionCommandValidator>();
        services.AddScoped<IImpersonationSessionService, ImpersonationSessionService>();

        return services;
    }
}
