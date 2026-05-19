using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Application.Platform.Validation;
using POS.Application.Fiscal;
using POS.Application.Fiscal.Validation;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Entitlements;
using POS.Infrastructure.Platform;
using POS.Infrastructure.Cash;
using POS.Infrastructure.Fiscal;
using POS.Infrastructure.Purchases;
using POS.Infrastructure.Sales;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Services;

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
        services.AddScoped<IFiscalAuthorizationService, ArcaFiscalAuthorizationService>();
        services.AddScoped<IIssueElectronicInvoiceHandler, IssueElectronicInvoiceHandler>();
        services.AddScoped<IRetryElectronicInvoiceHandler, RetryElectronicInvoiceHandler>();
        services.AddScoped<IIssueCreditNoteHandler, IssueCreditNoteHandler>();
        services.AddScoped<IValidator<IssueElectronicInvoiceCommand>, IssueElectronicInvoiceCommandValidator>();
        services.AddScoped<IValidator<RetryElectronicInvoiceCommand>, RetryElectronicInvoiceCommandValidator>();
        services.AddScoped<IValidator<IssueCreditNoteCommand>, IssueCreditNoteCommandValidator>();
        services.AddScoped<ICashSessionService, CashSessionService>();
        services.AddScoped<ISalesQueryService, SalesQueryService>();
        services.AddHostedService<FiscalRetryWorker>();

        services.AddScoped<IValidator<ProvisionPlatformUserCommand>, ProvisionPlatformUserCommandValidator>();
        services.AddScoped<IProvisionPlatformUserHandler, ProvisionPlatformUserHandler>();
        services.AddScoped<IPlatformAuditService, EfPlatformAuditService>();
        services.AddScoped<IPlatformAuditQueryService, PlatformAuditQueryService>();
        services.AddScoped<IPlatformMetricsOverviewQuery, PlatformMetricsOverviewQuery>();
        services.AddScoped<IPlatformDirectoryQuery, PlatformDirectoryQuery>();

        services.AddScoped<IValidator<CreatePlatformTenantCommand>, CreatePlatformTenantCommandValidator>();
        services.AddScoped<IValidator<UpdatePlatformTenantCommand>, UpdatePlatformTenantCommandValidator>();
        services.AddScoped<IValidator<SuspendPlatformTenantCommand>, SuspendPlatformTenantCommandValidator>();
        services.AddScoped<IValidator<ClosePlatformTenantCommand>, ClosePlatformTenantCommandValidator>();
        services.AddScoped<IPlatformTenantLifecycleService, PlatformTenantLifecycleService>();

        services.AddScoped<ITenantEntitlementGuard, TenantEntitlementGuard>();
        services.AddScoped<IValidator<SetTenantEntitlementsCommand>, SetTenantEntitlementsCommandValidator>();
        services.AddScoped<IPlatformTenantEntitlementsService, PlatformTenantEntitlementsService>();

        services.AddScoped<IValidator<PlatformUserActionRequest>, PlatformUserActionRequestValidator>();
        services.AddScoped<IPlatformTenantUserQuery, PlatformTenantUserQuery>();
        services.AddScoped<IPlatformTenantUserAdminService, PlatformTenantUserAdminService>();

        services.AddScoped<IValidator<StartImpersonationSessionCommand>, StartImpersonationSessionCommandValidator>();
        services.AddScoped<IImpersonationSessionService, ImpersonationSessionService>();

        return services;
    }
}
