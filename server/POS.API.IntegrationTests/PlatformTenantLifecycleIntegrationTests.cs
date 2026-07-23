using System.Net;
using System.Net.Http.Json;
using POS.Application.Contracts.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformTenantLifecycleIntegrationTests
{
    [Fact]
    public async Task Create_WithStarterPlan_SeedsEntitlements()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Negocio Starter",
                BusinessType = BusinessTypeNames.Farmacia,
                AdminEmail = $"starter-{Guid.NewGuid():N}@test.local",
                AdminPassword = "Pass123!",
                PlanCode = TenantPlanPresets.Starter
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

        var entRes = await platformClient.GetAsync($"/api/platform/tenants/{tenantId}/entitlements");
        Assert.Equal(HttpStatusCode.OK, entRes.StatusCode);
        var entitlements = await entRes.Content.ReadFromJsonAsync<ApiResponse<TenantEntitlementsDto>>();
        Assert.Equal(100, entitlements?.Data?.MaxProducts);
        Assert.Equal(3, entitlements?.Data?.MaxTenantUsers);
        Assert.True(entitlements?.Data?.SalesEnabled);
    }

    [Fact]
    public async Task Create_WithExplicitCaps_OverridesPlan()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Negocio override",
                AdminEmail = $"override-{Guid.NewGuid():N}@test.local",
                AdminPassword = "Pass123!",
                PlanCode = TenantPlanPresets.Pro,
                MaxProducts = 50,
                MaxTenantUsers = 2,
                SalesEnabled = false
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

        var entRes = await platformClient.GetAsync($"/api/platform/tenants/{tenantId}/entitlements");
        var entitlements = await entRes.Content.ReadFromJsonAsync<ApiResponse<TenantEntitlementsDto>>();
        Assert.Equal(50, entitlements?.Data?.MaxProducts);
        Assert.Equal(2, entitlements?.Data?.MaxTenantUsers);
        Assert.False(entitlements?.Data?.SalesEnabled);
    }

    [Fact]
    public async Task Create_WithAdmin_AllowsLogin_AndLifecycleFlow()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var adminEmail = $"admin-{Guid.NewGuid():N}@test.local";
        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Negocio ciclo",
                ContactEmail = "ops@test.local",
                BusinessType = BusinessTypeNames.Kiosco,
                AdminEmail = adminEmail,
                AdminFullName = "Admin Ciclo",
                AdminPassword = "Pass123!"
            });

        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(TenantStatus.Active, created!.Data!.Status);
        Assert.Equal(BusinessTypeNames.Kiosco, created.Data.BusinessType);
        var tenantId = created.Data.Id;

        using var loginClient = factory.CreateClient();
        var loginOk = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email = adminEmail, password = "Pass123!" });
        Assert.Equal(HttpStatusCode.OK, loginOk.StatusCode);

        var getRes = await platformClient.GetAsync($"/api/platform/tenants/{tenantId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var suspendRes = await platformClient.PostAsJsonAsync($"/api/platform/tenants/{tenantId}/suspend", new { });
        Assert.Equal(HttpStatusCode.OK, suspendRes.StatusCode);

        var suspendAgain = await platformClient.PostAsJsonAsync($"/api/platform/tenants/{tenantId}/suspend", new { });
        Assert.Equal(HttpStatusCode.OK, suspendAgain.StatusCode);

        var filtered = await platformClient.GetAsync("/api/platform/tenants?status=Suspended&nameContains=ciclo");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var filteredBody = await filtered.Content.ReadFromJsonAsync<ApiResponse<TenantDirectoryPageDto>>();
        Assert.Contains(filteredBody!.Data!.Items, x => x.Id == tenantId && x.Status == TenantStatus.Suspended);

        var loginSuspended = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email = adminEmail, password = "Pass123!" });
        Assert.Equal(HttpStatusCode.Forbidden, loginSuspended.StatusCode);

        var unsuspendRes = await platformClient.PostAsJsonAsync($"/api/platform/tenants/{tenantId}/unsuspend", new { });
        Assert.Equal(HttpStatusCode.OK, unsuspendRes.StatusCode);
        var unsuspended = await unsuspendRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        Assert.Equal(TenantStatus.Active, unsuspended!.Data!.Status);

        var loginAfterUnsuspend = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email = adminEmail, password = "Pass123!" });
        Assert.Equal(HttpStatusCode.OK, loginAfterUnsuspend.StatusCode);

        var closeRes = await platformClient.PostAsJsonAsync($"/api/platform/tenants/{tenantId}/close", new { });
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);

        var updateClosed = await platformClient.PatchAsJsonAsync(
            $"/api/platform/tenants/{tenantId}",
            new UpdateTenantApiRequest { Name = "No debería", ContactEmail = null });
        Assert.Equal(HttpStatusCode.BadRequest, updateClosed.StatusCode);

        var reopenRes = await platformClient.PostAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/reopen",
            new PlatformUserActionRequest { Justification = "Reapertura por error operativo" });
        Assert.Equal(HttpStatusCode.OK, reopenRes.StatusCode);
        var reopened = await reopenRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        Assert.Equal(TenantStatus.Active, reopened!.Data!.Status);

        var updateOk = await platformClient.PatchAsJsonAsync(
            $"/api/platform/tenants/{tenantId}",
            new UpdateTenantApiRequest { Name = "Negocio ciclo actualizado", ContactEmail = "ops2@test.local" });
        Assert.Equal(HttpStatusCode.OK, updateOk.StatusCode);
        var updated = await updateOk.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        Assert.Equal("Negocio ciclo actualizado", updated!.Data!.Name);
    }

    [Fact]
    public async Task Create_DuplicateAdminEmail_ReturnsBadRequest()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var adminEmail = $"dup-{Guid.NewGuid():N}@test.local";
        var payload = new CreateTenantApiRequest
        {
            Name = "Primero",
            BusinessType = BusinessTypeNames.Farmacia,
            AdminEmail = adminEmail,
            AdminPassword = "Pass123!"
        };

        var first = await platformClient.PostAsJsonAsync("/api/platform/tenants", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Segundo",
                BusinessType = BusinessTypeNames.Kiosco,
                AdminEmail = adminEmail,
                AdminPassword = "Pass123!"
            });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Login_Returns403_WhenTenantSuspended()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var email = $"user-{Guid.NewGuid():N}@test.local";
        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Negocio login",
                ContactEmail = null,
                AdminEmail = email,
                AdminFullName = "Tester",
                AdminPassword = "Pass123!"
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

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

        var email = $"user-closed-{Guid.NewGuid():N}@test.local";
        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Negocio cerrado login",
                ContactEmail = null,
                AdminEmail = email,
                AdminFullName = "Tester Closed",
                AdminPassword = "Pass123!"
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

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
