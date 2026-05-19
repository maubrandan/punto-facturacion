using System.Net;
using System.Net.Http.Json;
using POS.Application.Contracts.Platform;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformAuditIntegrationTests
{
    [Fact]
    public async Task ListAudit_IncludesTenantEntitlementsUpdated_AndFilterByTenant()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var createRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest { Name = $"Audit Tenant {Guid.NewGuid():N}", ContactEmail = null });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

        var setRes = await platformClient.PutAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/entitlements",
            new
            {
                maxProducts = 3,
                maxTenantUsers = 5,
                salesEnabled = true,
                justification = "Ajuste de plan por onboarding"
            });
        Assert.Equal(HttpStatusCode.OK, setRes.StatusCode);

        var listRes = await platformClient.GetAsync("/api/platform/audit?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<ApiResponse<PlatformAuditEventPageDto>>();
        Assert.NotNull(list?.Data);
        Assert.Contains(
            list!.Data!.Items,
            x => x.Action == "TenantEntitlementsUpdated" && x.AffectedTenantId == tenantId);

        var filteredRes = await platformClient.GetAsync($"/api/platform/audit?page=1&pageSize=20&tenantId={tenantId}");
        Assert.Equal(HttpStatusCode.OK, filteredRes.StatusCode);
        var filtered = await filteredRes.Content.ReadFromJsonAsync<ApiResponse<PlatformAuditEventPageDto>>();
        Assert.NotNull(filtered?.Data);
        Assert.NotEmpty(filtered!.Data!.Items);
        Assert.All(
            filtered.Data.Items,
            x => Assert.Equal(tenantId, x.AffectedTenantId));
    }

    private sealed class ApiResponse<T>
    {
        public bool Success { get; init; }

        public T? Data { get; init; }
    }
}
