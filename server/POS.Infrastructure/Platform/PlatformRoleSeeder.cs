using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using POS.Domain.Platform;

namespace POS.Infrastructure.Platform;

public static class PlatformRoleSeeder
{
    public static async Task EnsurePlatformRolesAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        foreach (var name in PlatformRoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(name))
                continue;
            var r = await roleManager.CreateAsync(new IdentityRole(name));
            if (!r.Succeeded)
            {
                var details = string.Join(" ", r.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"No se pudo crear el rol {name}: {details}");
            }

            logger.LogInformation("Rol de plataforma creado: {Role}", name);
        }
    }
}
