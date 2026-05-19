using System.Net;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformApiAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PlatformApiAuthorizationTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Tenants_WithoutAuth_Returns401()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");

        var response = await client.GetAsync("/api/platform/tenants?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tenants_WithTenantUser_Returns403()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-TenantId", $"tenant-{Guid.NewGuid():N}");

        var response = await client.GetAsync("/api/platform/tenants?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Audit_WithTenantUser_Returns403()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-TenantId", $"tenant-{Guid.NewGuid():N}");

        var response = await client.GetAsync("/api/platform/audit?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MetricsOverview_WithTenantUser_Returns403()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-TenantId", $"tenant-{Guid.NewGuid():N}");

        var response = await client.GetAsync("/api/platform/metrics/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenants_WithPlatformClaims_Returns200()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var response = await client.GetAsync("/api/platform/tenants?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MetricsOverview_WithPlatformClaims_Returns200()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var response = await client.GetAsync("/api/platform/metrics/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>JWT de suplantación no incluye <c>is_platform</c>: rutas de consola deben denegarse (Fase 7).</summary>
    [Fact]
    public async Task Tenants_WithImpersonationClaims_Returns403()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Impersonation", "true");
        client.DefaultRequestHeaders.Add("X-Test-TenantId", $"tenant-{Guid.NewGuid():N}");

        var response = await client.GetAsync("/api/platform/tenants?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Health_AllowsAnonymous_Returns200()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");

        var response = await client.GetAsync("/api/platform/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Response_IncludesRequestIdHeader()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");

        var response = await client.GetAsync("/api/platform/health");

        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var values));
        Assert.NotEmpty(values);
    }
}
