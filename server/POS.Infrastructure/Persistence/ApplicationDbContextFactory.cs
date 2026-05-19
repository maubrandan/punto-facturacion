using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Factory para migraciones (<c>dotnet ef</c>) sin pipeline HTTP.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        private readonly string _tenantId;

        public DesignTimeCurrentUserService(string tenantId) => _tenantId = tenantId;

        public string? TenantId => _tenantId;

        public string? UserId => null;

        public bool IsPlatformContext => false;

        public bool IsImpersonationContext => false;
    }

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var apiBase = ResolveApiProjectDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(apiBase)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' no está configurada (appsettings de POS.API).");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        ICurrentUserService currentUser = new DesignTimeCurrentUserService("__design_time__");

        return new ApplicationDbContext(options, currentUser);
    }

    private static string ResolveApiProjectDirectory()
    {
        var cwd = Path.GetFullPath(Directory.GetCurrentDirectory());
        var candidates = new[]
        {
            cwd,
            Path.Combine(cwd, "POS.API"),
            Path.Combine(cwd, "..", "POS.API"),
            Path.Combine(cwd, "..", "..", "POS.API"),
        };

        foreach (var dir in candidates.Select(Path.GetFullPath).Distinct())
        {
            if (File.Exists(Path.Combine(dir, "appsettings.json")))
                return dir;
        }

        throw new InvalidOperationException(
            "No se encontró appsettings.json de POS.API. Ejecute las migraciones con --startup-project POS.API desde la carpeta server.");
    }
}
