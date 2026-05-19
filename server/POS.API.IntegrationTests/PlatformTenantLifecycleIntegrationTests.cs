using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Contracts.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformTenantLifecycleIntegrationTests
{
    [Fact]
    public async Task Create_Get_List_Filter_Suspend_IdempotentFlow()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest { Name = "Negocio ciclo", ContactEmail = "ops@test.local" });

        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(TenantStatus.Active, created!.Data!.Status);
        var tenantId = created.Data.Id;

        var getRes = await platformClient.GetAsync($"/api/platform/tenants/{tenantId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var suspendedOnly = await platformClient.GetAsync("/api/platform/tenants?status=Suspended&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, suspendedOnly.StatusCode);
        var suspendedBody = await suspendedOnly.Content.ReadFromJsonAsync<ApiResponse<TenantDirectoryPageDto>>();
        Assert.NotNull(suspendedBody?.Data);
        Assert.DoesNotContain(suspendedBody!.Data!.Items, x => x.Id == tenantId);

        var suspendRes = await platformClient.PostAsJsonAsync($"/api/platform/tenants/{tenantId}/suspend", new { });
        Assert.Equal(HttpStatusCode.OK, suspendRes.StatusCode);

        var suspendAgain = await platformClient.PostAsJsonAsync($"/api/platform/tenants/{tenantId}/suspend", new { });
        Assert.Equal(HttpStatusCode.OK, suspendAgain.StatusCode);

        var filtered = await platformClient.GetAsync("/api/platform/tenants?status=Suspended&nameContains=ciclo");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var filteredBody = await filtered.Content.ReadFromJsonAsync<ApiResponse<TenantDirectoryPageDto>>();
        Assert.Contains(filteredBody!.Data!.Items, x => x.Id == tenantId && x.Status == TenantStatus.Suspended);
    }

    [Fact]
    public async Task Login_Returns403_WhenTenantSuspended()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest { Name = "Negocio login", ContactEmail = null });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

        var email = $"user-{Guid.NewGuid():N}@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var appUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = tenantId,
                FullName = "Tester",
                BusinessType = "Kiosco",
                AccountKind = UserAccountKind.TenantUser
            };
            var cr = await users.CreateAsync(appUser, "Pass123!");
            Assert.True(cr.Succeeded);
        }

        var suspendRes = await platformClient.PostAsJsonAsync($"/api/platform/tenants/{tenantId}/suspend", new { });
        Assert.Equal(HttpStatusCode.OK, suspendRes.StatusCode);

        using var loginClient = factory.CreateClient();
        var loginRes = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Pass123!" });

        Assert.Equal(HttpStatusCode.Forbidden, loginRes.StatusCode);
    }

    [Fact]
    public async Task Login_Returns403_WhenTenantClosed()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest { Name = "Negocio cerrado login", ContactEmail = null });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

        var email = $"user-closed-{Guid.NewGuid():N}@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var appUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = tenantId,
                FullName = "Tester Closed",
                BusinessType = "Kiosco",
                AccountKind = UserAccountKind.TenantUser
            };
            var cr = await users.CreateAsync(appUser, "Pass123!");
            Assert.True(cr.Succeeded);
        }

        var closeRes = await platformClient.PostAsJsonAsync($"/api/platform/tenants/{tenantId}/close", new { });
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);

        using var loginClient = factory.CreateClient();
        var loginRes = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Pass123!" });

        Assert.Equal(HttpStatusCode.Forbidden, loginRes.StatusCode);
    }

    private sealed class ApiResponse<T>
    {
        public bool Success { get; init; }

        public T? Data { get; init; }
    }
}
