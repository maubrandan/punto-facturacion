using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformTenantSubscriptionIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Create_WithStarterPlan_SeedsSubscription()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Sub Starter",
                BusinessType = BusinessTypeNames.Farmacia,
                AdminEmail = $"sub-starter-{Guid.NewGuid():N}@test.local",
                AdminPassword = "Pass123!",
                PlanCode = TenantPlanPresets.Starter
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>(JsonOpts);
        var tenantId = created!.Data!.Id;

        var subRes = await platformClient.GetAsync($"/api/platform/tenants/{tenantId}/subscription");
        Assert.Equal(HttpStatusCode.OK, subRes.StatusCode);
        var sub = await subRes.Content.ReadFromJsonAsync<ApiResponse<TenantSubscriptionDto>>(JsonOpts);
        Assert.True(sub?.Success);
        Assert.Equal(TenantPlanPresets.Starter, sub!.Data!.PlanCode);
        Assert.Equal(SubscriptionStatus.Active, sub.Data.Status);
        Assert.Equal(BillingProvider.Manual, sub.Data.Provider);
        Assert.Equal(BillingCycle.Monthly, sub.Data.BillingCycle);
        Assert.True(sub.Data.CurrentPeriodEndUtc > sub.Data.CurrentPeriodStartUtc);
        Assert.True(sub.Data.EntitlementsMatchPlan);
    }

    [Fact]
    public async Task ChangePlan_ToPro_OverwritesEntitlements_AndAudits()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);

        var putRes = await platformClient.PutAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/subscription",
            new
            {
                planCode = TenantPlanPresets.Pro,
                status = SubscriptionStatus.Active,
                billingCycle = BillingCycle.Monthly,
                justification = "Upgrade a Pro por contrato"
            });
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        var sub = await putRes.Content.ReadFromJsonAsync<ApiResponse<TenantSubscriptionDto>>(JsonOpts);
        Assert.Equal(TenantPlanPresets.Pro, sub?.Data?.PlanCode);
        Assert.True(sub?.Data?.EntitlementsMatchPlan);

        var entRes = await platformClient.GetAsync($"/api/platform/tenants/{tenantId}/entitlements");
        var entitlements = await entRes.Content.ReadFromJsonAsync<ApiResponse<TenantEntitlementsDto>>(JsonOpts);
        Assert.Equal(2000, entitlements?.Data?.MaxProducts);
        Assert.Equal(20, entitlements?.Data?.MaxTenantUsers);
        Assert.Equal(TenantPlanPresets.Pro, entitlements?.Data?.MatchedPlanCode);

        var auditRes = await platformClient.GetAsync($"/api/platform/audit?page=1&pageSize=50&tenantId={tenantId}");
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
        var audit = await auditRes.Content.ReadFromJsonAsync<ApiResponse<PlatformAuditEventPageDto>>(JsonOpts);
        Assert.Contains(
            audit!.Data!.Items,
            x => x.Action == "TenantSubscriptionPlanChanged" && x.AffectedTenantId == tenantId);
    }

    [Fact]
    public async Task ChangePlan_UnknownPlan_Returns400()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);

        var putRes = await platformClient.PutAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/subscription",
            new
            {
                planCode = "EnterprisePlus",
                status = SubscriptionStatus.Active,
                billingCycle = BillingCycle.Monthly,
                justification = "Plan inventado inválido"
            });
        Assert.Equal(HttpStatusCode.BadRequest, putRes.StatusCode);
        var body = await putRes.Content.ReadFromJsonAsync<ApiResponse<TenantSubscriptionDto>>(JsonOpts);
        Assert.False(body?.Success);
        Assert.Equal("subscription.validation", body?.Error?.Code);
    }

    [Fact]
    public async Task CustomEntitlements_DoNotChangeSubscriptionPlan()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Pro);

        var putEnt = await platformClient.PutAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/entitlements",
            new
            {
                maxProducts = 42,
                maxTenantUsers = 7,
                salesEnabled = true,
                justification = "Caps custom sin tocar plan"
            });
        Assert.Equal(HttpStatusCode.OK, putEnt.StatusCode);

        var subRes = await platformClient.GetAsync($"/api/platform/tenants/{tenantId}/subscription");
        var sub = await subRes.Content.ReadFromJsonAsync<ApiResponse<TenantSubscriptionDto>>(JsonOpts);
        Assert.Equal(TenantPlanPresets.Pro, sub?.Data?.PlanCode);
        Assert.False(sub?.Data?.EntitlementsMatchPlan);
        Assert.Null(sub?.Data?.MatchedPlanCode);
    }

    [Fact]
    public async Task TenantAdmin_CanReadOwnSubscription_Isolated()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var tenantA = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);
        var tenantB = await CreateTenantAsync(platformClient, TenantPlanPresets.Unlimited);

        using var tenantClient = factory.CreateClient();
        tenantClient.DefaultRequestHeaders.Add("X-Test-TenantId", tenantA);

        var mine = await tenantClient.GetAsync("/api/tenant/subscription");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var body = await mine.Content.ReadFromJsonAsync<ApiResponse<TenantSubscriptionDto>>(JsonOpts);
        Assert.Equal(tenantA, body?.Data?.TenantId);
        Assert.Equal(TenantPlanPresets.Starter, body?.Data?.PlanCode);
        Assert.NotEqual(tenantB, body?.Data?.TenantId);
    }

    [Fact]
    public async Task TenantUser_CannotAccessPlatformSubscription_403()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);

        using var tenantClient = factory.CreateClient();
        tenantClient.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var res = await tenantClient.GetAsync($"/api/platform/tenants/{tenantId}/subscription");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task SupportReadOnly_CanGet_CannotChangePlan()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var opsClient = factory.CreateClient();
        opsClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var tenantId = await CreateTenantAsync(opsClient, TenantPlanPresets.Starter);

        using var supportClient = factory.CreateClient();
        supportClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        supportClient.DefaultRequestHeaders.Add("X-Test-PlatformRole", PlatformRoleNames.SupportReadOnly);

        var getRes = await supportClient.GetAsync($"/api/platform/tenants/{tenantId}/subscription");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var putRes = await supportClient.PutAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/subscription",
            new
            {
                planCode = TenantPlanPresets.Pro,
                status = SubscriptionStatus.Active,
                billingCycle = BillingCycle.Monthly,
                justification = "Support no debería mutar"
            });
        Assert.Equal(HttpStatusCode.Forbidden, putRes.StatusCode);
    }

    private static async Task<string> CreateTenantAsync(HttpClient platformClient, string planCode)
    {
        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = $"Sub {planCode} {Guid.NewGuid():N}"[..40],
                BusinessType = BusinessTypeNames.Kiosco,
                AdminEmail = $"sub-{Guid.NewGuid():N}@test.local",
                AdminPassword = "Pass123!",
                PlanCode = planCode
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>(JsonOpts);
        return created!.Data!.Id;
    }
}
