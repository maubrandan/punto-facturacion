using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Billing;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Billing;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class SaaSBillingExpansionIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task SelfServeUpgrade_IsolatesTenant_AndCreatesInvoice()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var tenantA = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);
        var tenantB = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);

        using var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Test-TenantId", tenantA);

        var upgrade = await clientA.PostAsJsonAsync(
            "/api/tenant/subscription/upgrade",
            new { planCode = TenantPlanPresets.Pro, billingCycle = BillingCycle.Monthly });
        Assert.Equal(HttpStatusCode.OK, upgrade.StatusCode);
        var body = await upgrade.Content.ReadFromJsonAsync<ApiResponse<SelfServeUpgradePayload>>(JsonOpts);
        Assert.True(body?.Success);
        Assert.True(body!.Data!.AppliedImmediately);
        Assert.Equal(TenantPlanPresets.Pro, body.Data.Subscription.PlanCode);
        Assert.NotNull(body.Data.Invoice);
        Assert.Equal(tenantA, body.Data.Invoice!.TenantId);

        var invoices = await clientA.GetAsync("/api/tenant/invoices");
        Assert.Equal(HttpStatusCode.OK, invoices.StatusCode);
        var list = await invoices.Content.ReadFromJsonAsync<ApiResponse<SubscriptionInvoiceListDto>>(JsonOpts);
        Assert.True(list?.Data?.TotalCount >= 1);
        Assert.All(list!.Data!.Items, i => Assert.Equal(tenantA, i.TenantId));

        using var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Test-TenantId", tenantB);
        var invoicesB = await clientB.GetAsync("/api/tenant/invoices");
        var listB = await invoicesB.Content.ReadFromJsonAsync<ApiResponse<SubscriptionInvoiceListDto>>(JsonOpts);
        Assert.Equal(0, listB?.Data?.TotalCount ?? -1);

        var subB = await clientB.GetAsync("/api/tenant/subscription");
        var subBBody = await subB.Content.ReadFromJsonAsync<ApiResponse<TenantSubscriptionDto>>(JsonOpts);
        Assert.Equal(TenantPlanPresets.Starter, subBBody?.Data?.PlanCode);
    }

    [Fact]
    public async Task SelfServeUpgrade_Downgrade_Returns400()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Pro);

        using var tenantClient = factory.CreateClient();
        tenantClient.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var res = await tenantClient.PostAsJsonAsync(
            "/api/tenant/subscription/upgrade",
            new { planCode = TenantPlanPresets.Starter, billingCycle = BillingCycle.Monthly });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<SelfServeUpgradePayload>>(JsonOpts);
        Assert.Equal("subscription.upgrade.downgrade_forbidden", body?.Error?.Code);
    }

    [Fact]
    public async Task ProviderNone_BlocksSelfServeUpgrade()
    {
        await using var factory = new TestWebApplicationFactory(billingProvider: "None");
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);

        using var tenantClient = factory.CreateClient();
        tenantClient.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var res = await tenantClient.PostAsJsonAsync(
            "/api/tenant/subscription/upgrade",
            new { planCode = TenantPlanPresets.Pro, billingCycle = BillingCycle.Monthly });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<SelfServeUpgradePayload>>(JsonOpts);
        Assert.Equal("subscription.provider_disabled", body?.Error?.Code);
    }

    [Fact]
    public async Task RenewalJob_ExtendsPeriod_AndCreatesPaidInvoice()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);

        DateTime previousEnd;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.TenantSubscriptions.FindAsync([tenantId]);
            Assert.NotNull(sub);
            previousEnd = DateTime.UtcNow.AddDays(-1);
            sub!.CurrentPeriodStartUtc = previousEnd.AddMonths(-1);
            sub.CurrentPeriodEndUtc = previousEnd;
            sub.Status = SubscriptionStatus.Active;
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<ISubscriptionBillingJobs>();
            var n = await jobs.ProcessRenewalsAsync();
            Assert.True(n >= 1);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.TenantSubscriptions.FindAsync([tenantId]);
            Assert.NotNull(sub);
            Assert.Equal(SubscriptionStatus.Active, sub!.Status);
            Assert.True(sub.CurrentPeriodEndUtc > previousEnd);
            Assert.True(
                await db.SubscriptionInvoices.IgnoreQueryFilters()
                    .AnyAsync(i => i.TenantId == tenantId && i.Status == SubscriptionInvoiceStatus.Paid));
        }
    }

    [Fact]
    public async Task DunningJob_CancelsAfterGrace_WhenPastDue()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.TenantSubscriptions.FindAsync([tenantId]);
            Assert.NotNull(sub);
            var now = DateTime.UtcNow;
            sub!.Status = SubscriptionStatus.PastDue;
            sub.PastDueSinceUtc = now.AddDays(-10);
            sub.GracePeriodEndsAtUtc = now.AddMinutes(-1);
            sub.DunningAttemptCount = 3;
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<ISubscriptionBillingJobs>();
            var n = await jobs.ProcessDunningAsync();
            Assert.True(n >= 1);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.TenantSubscriptions.FindAsync([tenantId]);
            Assert.Equal(SubscriptionStatus.Canceled, sub?.Status);
            Assert.NotNull(sub?.CanceledAtUtc);
        }
    }

    [Fact]
    public async Task MarkPastDue_ViaDomain_ThenDunningAttempt()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var tenantId = await CreateTenantAsync(platformClient, TenantPlanPresets.Starter);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.TenantSubscriptions.FindAsync([tenantId]);
            Assert.NotNull(sub);
            sub!.MarkPastDue(DateTime.UtcNow, TimeSpan.FromDays(7));
            await db.SaveChangesAsync();
            Assert.Equal(SubscriptionStatus.PastDue, sub.Status);
            Assert.NotNull(sub.GracePeriodEndsAtUtc);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<ISubscriptionBillingJobs>();
            await jobs.ProcessDunningAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.TenantSubscriptions.FindAsync([tenantId]);
            Assert.Equal(SubscriptionStatus.PastDue, sub?.Status);
            Assert.True(sub!.DunningAttemptCount >= 1);
        }
    }

    private static async Task<string> CreateTenantAsync(HttpClient platformClient, string planCode)
    {
        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = $"Bill {planCode} {Guid.NewGuid():N}"[..40],
                BusinessType = BusinessTypeNames.Kiosco,
                AdminEmail = $"bill-{Guid.NewGuid():N}@test.local",
                AdminPassword = "Pass123!",
                PlanCode = planCode
            });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>(JsonOpts);
        return created!.Data!.Id;
    }

    private sealed class SelfServeUpgradePayload
    {
        public TenantSubscriptionDto Subscription { get; set; } = null!;
        public SubscriptionInvoiceDto? Invoice { get; set; }
        public bool AppliedImmediately { get; set; }
        public string? CheckoutUrl { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
