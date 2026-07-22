using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Domain.Tenant;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class CustomersIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Admin_CanCrudCustomer_Cashier_CanSearch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-cust-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        admin.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        var createRes = await admin.PostAsJsonAsync(
            "/api/customers",
            new
            {
                name = "Cliente SA",
                taxId = "30-71234567-1",
                email = "cli@test.local",
                phone = "111",
                address = "Calle 1"
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(JsonOpts);
        Assert.True(created!.Success);
        Assert.Equal("30712345671", created.Data!.TaxId);
        var id = created.Data.Id;

        using var cashier = factory.CreateClient();
        cashier.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        cashier.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Cashier);

        var search = await cashier.GetAsync("/api/customers?q=Cliente");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var list = await search.Content.ReadFromJsonAsync<ApiResponse<List<CustomerDto>>>(JsonOpts);
        Assert.True(list!.Success);
        Assert.Contains(list.Data!, c => c.Id == id);

        var cashierCreate = await cashier.PostAsJsonAsync(
            "/api/customers",
            new { name = "X", taxId = "20111111112" });
        Assert.Equal(HttpStatusCode.Forbidden, cashierCreate.StatusCode);

        var updateRes = await admin.PutAsJsonAsync(
            $"/api/customers/{id}",
            new
            {
                name = "Cliente SA Actualizado",
                taxId = "30712345671",
                email = "nuevo@test.local",
                phone = "222",
                address = "Calle 2"
            });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var deleteRes = await admin.DeleteAsync($"/api/customers/{id}");
        Assert.Equal(HttpStatusCode.OK, deleteRes.StatusCode);

        var missing = await cashier.GetAsync($"/api/customers/{id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Customers_AreIsolated_ByTenant()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantA = $"t-ca-{Guid.NewGuid():N}";
        var tenantB = $"t-cb-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantA);
        await SeedTenantAsync(factory, tenantB);

        using var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Test-TenantId", tenantA);
        clientA.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        var createA = await clientA.PostAsJsonAsync(
            "/api/customers",
            new { name = "Solo A", taxId = "20123456789", email = "", phone = "", address = "" });
        Assert.Equal(HttpStatusCode.Created, createA.StatusCode);
        var bodyA = await createA.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(JsonOpts);
        var idA = bodyA!.Data!.Id;

        using var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Test-TenantId", tenantB);
        clientB.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        var listB = await clientB.GetAsync("/api/customers");
        var bodyB = await listB.Content.ReadFromJsonAsync<ApiResponse<List<CustomerDto>>>(JsonOpts);
        Assert.DoesNotContain(bodyB!.Data!, c => c.Id == idA);

        var getCross = await clientB.GetAsync($"/api/customers/{idA}");
        Assert.Equal(HttpStatusCode.NotFound, getCross.StatusCode);
    }

    [Fact]
    public async Task DuplicateTaxId_SameTenant_IsRejected()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-dup-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        var first = await client.PostAsJsonAsync(
            "/api/customers",
            new { name = "Uno", taxId = "20999888777", email = "", phone = "", address = "" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/api/customers",
            new { name = "Dos", taxId = "20-99988877-7", email = "", phone = "", address = "" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var err = await second.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("customer.duplicate", err!.Error?.Code);
    }

    private static async Task SeedTenantAsync(TestWebApplicationFactory factory, string tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Tenants.Add(
            new Tenant
            {
                Id = tenantId,
                Name = tenantId,
                BusinessType = BusinessTypeNames.Kiosco,
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
        public string? Message { get; set; }
    }

    private sealed class CustomerDto
    {
        public Guid Id { get; set; }
        public string TaxId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
