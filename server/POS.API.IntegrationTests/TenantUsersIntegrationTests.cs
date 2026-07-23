using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Domain.Tenant;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.TenantUsers;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class TenantUsersIntegrationTests
{
    [Fact]
    public async Task Cashier_CannotListUsers_AdminCanCreateCashier()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-users-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId, BusinessTypeNames.Kiosco);

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        adminClient.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var log = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger("test");
            await TenantRoleSeeder.EnsureTenantRolesAsync(roles, log);
        }

        var createRes = await adminClient.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = $"cajero-{Guid.NewGuid():N}@test.local",
                password = "Pass123!",
                fullName = "Cajero Uno",
                role = TenantRoleNames.Cashier
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        using var cashierClient = factory.CreateClient();
        cashierClient.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        cashierClient.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Cashier);

        var listForbidden = await cashierClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, listForbidden.StatusCode);

        var listOk = await adminClient.GetAsync("/api/users?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listOk.StatusCode);
    }

    [Fact]
    public async Task CreateUser_RespectsMaxTenantUsers()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-quota-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId, BusinessTypeNames.Kiosco);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var log = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger("test");
            await TenantRoleSeeder.EnsureTenantRolesAsync(roles, log);

            db.TenantEntitlements.Add(
                new TenantEntitlement
                {
                    TenantId = tenantId,
                    MaxTenantUsers = 1,
                    MaxProducts = null,
                    SalesEnabled = true,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            await db.SaveChangesAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var email = $"admin-{Guid.NewGuid():N}@test.local";
            var existing = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = tenantId,
                FullName = "Admin",
                BusinessType = BusinessTypeNames.Kiosco,
                AccountKind = UserAccountKind.TenantUser
            };
            Assert.True((await users.CreateAsync(existing, "Pass123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(existing, TenantRoleNames.Admin)).Succeeded);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        var createRes = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = $"extra-{Guid.NewGuid():N}@test.local",
                password = "Pass123!",
                fullName = "Extra",
                role = TenantRoleNames.Cashier
            });
        Assert.Equal(HttpStatusCode.BadRequest, createRes.StatusCode);
        var body = await createRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("entitlement.user_limit_reached", body!.Error?.Code);
    }

    [Fact]
    public async Task RequestPasswordReset_SendsEmail()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        factory.EmailSender.Clear();

        var tenantId = $"t-reset-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId, BusinessTypeNames.Kiosco);

        string userId;
        var email = $"reset-{Guid.NewGuid():N}@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var log = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger("test");
            await TenantRoleSeeder.EnsureTenantRolesAsync(roles, log);

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = tenantId,
                FullName = "Reset Target",
                BusinessType = BusinessTypeNames.Kiosco,
                AccountKind = UserAccountKind.TenantUser
            };
            Assert.True((await users.CreateAsync(user, "Pass123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, TenantRoleNames.Cashier)).Succeeded);
            userId = user.Id;
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        var res = await client.PostAsync($"/api/users/{userId}/request-password-reset", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var sent = factory.EmailSender.Sent;
        Assert.Single(sent);
        Assert.Equal(email, sent[0].To);
        Assert.Contains("Token:", sent[0].PlainTextBody, StringComparison.Ordinal);
    }

    private static async Task SeedTenantAsync(
        TestWebApplicationFactory factory,
        string tenantId,
        string businessType)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Tenants.Add(
            new Tenant
            {
                Id = tenantId,
                Name = "Users Test",
                BusinessType = businessType,
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
    }

    private sealed class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public ApiError? Error { get; set; }
    }

    private sealed class ApiError
    {
        public string? Code { get; set; }
    }
}
