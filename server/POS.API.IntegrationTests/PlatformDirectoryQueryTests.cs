using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformDirectoryQueryTests
{
    [Fact]
    public async Task ListAllTenantsAsync_IncludesInsertedTenant()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ICurrentUserTenantContext>();
        tenantContext.OverriddenTenantId = "ctx-tenant";

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = $"t-dir-{Guid.NewGuid():N}";
        db.Tenants.Add(new Tenant { Id = id, Name = "Dir One", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var query = scope.ServiceProvider.GetRequiredService<IPlatformDirectoryQuery>();
        var list = await query.ListAllTenantsAsync(CancellationToken.None);

        Assert.Contains(list, t => t.Id == id && t.Name == "Dir One");
    }
}
