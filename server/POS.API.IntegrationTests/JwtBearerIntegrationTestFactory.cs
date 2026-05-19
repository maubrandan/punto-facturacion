using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

/// <summary>
/// Host de prueba con JWT real (sin <see cref="TestWebApplicationFactory"/> TestAuth).
/// Útil para validar claims emitidos por <c>CreatePlatformToken</c> / <c>CreateImpersonationToken</c>.
/// </summary>
public sealed class JwtBearerIntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _testDbName = $"PosFacturacion_JwtBearerTests_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting tiene prioridad sobre appsettings del API: misma clave que IOptions<JwtOptions> y que Program (JwtBearer).
        builder.UseSetting("AdminSeed:Enabled", "false");
        builder.UseSetting("PlatformAdminSeed:Enabled", "false");
        builder.UseSetting("Jwt:Issuer", "POS");
        builder.UseSetting("Jwt:Audience", "pos-clients");
        builder.UseSetting("Jwt:SigningKey", "TESTING-SIGNING-KEY-MINIMUM-32-CHARS!!");
        builder.UseSetting("ConnectionStrings:DefaultConnection", BuildConnectionString());

        builder.ConfigureServices(
            services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));

                services.AddDbContext<ApplicationDbContext>(
                    options => options.UseSqlServer(BuildConnectionString()));
            });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
    }

    private string BuildConnectionString() =>
        $"Server=(localdb)\\mssqllocaldb;Database={_testDbName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";
}
