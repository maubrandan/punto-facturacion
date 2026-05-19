using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Platform;

namespace POS.Infrastructure.Persistence;

public static class DbInitializer
{
    private static readonly HashSet<string> AllowedBusinessTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Farmacia",
        "Ferreteria",
        "Kiosco"
    };

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tenantContext = serviceProvider.GetRequiredService<ICurrentUserTenantContext>();
        var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        var platformOptions = serviceProvider.GetRequiredService<IOptions<PlatformAdminSeedOptions>>().Value;

        await context.Database.MigrateAsync(cancellationToken);

        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await PlatformRoleSeeder.EnsurePlatformRolesAsync(roleManager, logger, cancellationToken);

        if (adminOptions.Enabled)
        {
            var adminEmail = adminOptions.Email.Trim();
            if (await userManager.FindByEmailAsync(adminEmail) is not null)
            {
                logger.LogInformation("Usuario admin de negocio ya existe: {Email}", adminEmail);
            }
            else
            {
                var businessType = NormalizeBusinessType(adminOptions.BusinessType);
                var fullName = string.IsNullOrWhiteSpace(adminOptions.FullName)
                    ? "Administrador Sistema"
                    : adminOptions.FullName.Trim();
                var password = adminOptions.Password;
                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException("AdminSeed:Password es obligatorio cuando el seed está habilitado.");
                var businessName = string.IsNullOrWhiteSpace(adminOptions.BusinessName)
                    ? "Administracion (seed)"
                    : adminOptions.BusinessName.Trim();

                var tenantId = Guid.NewGuid().ToString("N");
                tenantContext.OverriddenTenantId = tenantId;

                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = fullName,
                    BusinessType = businessType,
                    TenantId = tenantId,
                    AccountKind = UserAccountKind.TenantUser
                };

                try
                {
                    await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

                    var createResult = await userManager.CreateAsync(adminUser, password);
                    if (!createResult.Succeeded)
                    {
                        var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
                        throw new InvalidOperationException($"No se pudo crear el usuario admin: {errors}");
                    }

                    context.Tenants.Add(
                        new Tenant
                        {
                            Id = tenantId,
                            Name = businessName,
                            Status = TenantStatus.Active,
                            CreatedAt = DateTime.UtcNow
                        });
                    await context.SaveChangesAsync(cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                    logger.LogInformation("Usuario admin de negocio creado: {Email}", adminEmail);
                }
                finally
                {
                    tenantContext.OverriddenTenantId = null;
                }
            }
        }
        else
        {
            logger.LogInformation("AdminSeed (negocio) deshabilitado: no se crea usuario/tenant iniciales.");
        }

        if (platformOptions.Enabled)
        {
            var platformEmail = platformOptions.Email.Trim();
            if (await userManager.FindByEmailAsync(platformEmail) is not null)
            {
                logger.LogInformation("Operador de plataforma (seed) ya existe: {Email}", platformEmail);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(platformOptions.Password))
                    throw new InvalidOperationException("PlatformAdminSeed:Password es obligatorio si PlatformAdminSeed está habilitado.");

                if (!PlatformRoleNames.IsKnownRole(platformOptions.Role))
                    throw new InvalidOperationException("PlatformAdminSeed:Role debe ser un rol Platform.* registrado (PlatformRoleNames).");

                var platformHandler = serviceProvider.GetRequiredService<IProvisionPlatformUserHandler>();
                var provision = new ProvisionPlatformUserCommand(
                    platformEmail,
                    platformOptions.Password,
                    string.IsNullOrWhiteSpace(platformOptions.FullName) ? platformEmail : platformOptions.FullName.Trim(),
                    platformOptions.Role.Trim());
                var result = await platformHandler.HandleAsync(provision, cancellationToken);
                if (!result.IsSuccess)
                {
                    logger.LogWarning("No se pudo crear operador de plataforma: {Code} {Msg}", result.ErrorCode, result.Error);
                }
                else if (result.Value is { } v)
                {
                    logger.LogInformation("Operador de plataforma (seed) creado: {Email} rol {Role}", v.Email, v.AssignedRole);
                }
            }
        }
    }

    private static string NormalizeBusinessType(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "Farmacia" : value.Trim();
        if (!AllowedBusinessTypes.Contains(candidate))
            throw new InvalidOperationException("AdminSeed:BusinessType debe ser Farmacia, Ferreteria o Kiosco.");

        if (candidate.Equals("Farmacia", StringComparison.OrdinalIgnoreCase))
            return "Farmacia";
        if (candidate.Equals("Ferreteria", StringComparison.OrdinalIgnoreCase))
            return "Ferreteria";
        return "Kiosco";
    }
}
