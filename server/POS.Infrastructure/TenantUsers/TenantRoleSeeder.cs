using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using POS.Domain.Tenant;

namespace POS.Infrastructure.TenantUsers;

public static class TenantRoleSeeder
{
    public static async Task EnsureTenantRolesAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        foreach (var name in TenantRoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(name))
                continue;

            var r = await roleManager.CreateAsync(new IdentityRole(name));
            if (!r.Succeeded)
            {
                var details = string.Join(" ", r.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"No se pudo crear el rol {name}: {details}");
            }

            logger.LogInformation("Rol de tenant creado: {Role}", name);
        }
    }
}
