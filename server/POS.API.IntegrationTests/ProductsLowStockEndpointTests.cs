using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class ProductsLowStockEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ProductsLowStockEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLowStock_ReturnsAtMostFiveOrderedByStockThenName()
    {
        var tenant = $"t-lowstock-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        // Stocks 1,1,2,3,4,5 — top 5: both con stock 1 (orden alfabético: A-lfa antes que Z-eta), luego 2,3,4
        await CreateProductAsync(client, "Zeta S1", "SKU-LS-01", 1);
        await CreateProductAsync(client, "Alfa S1", "SKU-LS-02", 1);
        await CreateProductAsync(client, "M Stock2", "SKU-LS-03", 2);
        await CreateProductAsync(client, "M Stock3", "SKU-LS-04", 3);
        await CreateProductAsync(client, "M Stock4", "SKU-LS-05", 4);
        await CreateProductAsync(client, "M Stock5", "SKU-LS-06", 5);

        var response = await client.GetAsync("/api/products/low-stock");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(5, body.Data!.Count);
        Assert.Equal("Alfa S1", body.Data[0].Name);
        Assert.Equal(1, body.Data[0].Stock);
        Assert.Equal("Zeta S1", body.Data[1].Name);
        Assert.Equal(1, body.Data[1].Stock);
        Assert.Equal("M Stock2", body.Data[2].Name);
        Assert.Equal(2, body.Data[2].Stock);
        Assert.Equal("M Stock3", body.Data[3].Name);
        Assert.Equal("M Stock4", body.Data[4].Name);
    }

    [Fact]
    public async Task GetLowStock_RespectsCountQuery()
    {
        var tenant = $"t-lowstock-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        await CreateProductAsync(client, "P1", "SKU-LS-10", 1);
        await CreateProductAsync(client, "P2", "SKU-LS-11", 2);
        await CreateProductAsync(client, "P3", "SKU-LS-12", 3);

        var response = await client.GetAsync("/api/products/low-stock?count=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Data!.Count);
        Assert.Equal(1, body.Data[0].Stock);
        Assert.Equal(2, body.Data[1].Stock);
    }

    [Fact]
    public async Task GetLowStock_WhenCountBelowOne_IsClampedToOne()
    {
        var tenant = $"t-lowstock-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        await CreateProductAsync(client, "Solo", "SKU-LS-20", 7);
        await CreateProductAsync(client, "Otro", "SKU-LS-21", 1);

        var response = await client.GetAsync("/api/products/low-stock?count=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>();
        Assert.NotNull(body);
        var items = body!.Data!;
        Assert.Single(items);
        Assert.Equal("Otro", items[0].Name);
        Assert.Equal(1, items[0].Stock);
    }

    [Fact]
    public async Task GetLowStock_ExcludesOtherTenantsProducts()
    {
        var ownerTenant = $"t-lowstock-owner-{Guid.NewGuid():N}";
        using var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Add("X-Test-TenantId", ownerTenant);
        await CreateProductAsync(ownerClient, "Ajeno bajo", "SKU-LS-30", 0);

        var otherTenant = $"t-lowstock-other-{Guid.NewGuid():N}";
        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Test-TenantId", otherTenant);
        await CreateProductAsync(otherClient, "Local bajo", "SKU-LS-31", 1);

        var response = await otherClient.GetAsync("/api/products/low-stock");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Single(body.Data!);
        Assert.Equal("Local bajo", body.Data[0].Name);
    }

    private static async Task<Guid> CreateProductAsync(
        HttpClient client,
        string name,
        string sku,
        int stock)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name,
                sku,
                barcode = "7791234567890",
                netPrice = 100m,
                taxRate = 21m,
                stock,
                extendedDataJson = new { }
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var body = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProductData>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        return body.Data!.Id;
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

    private sealed class ProductData
    {
        public Guid Id { get; set; }
    }

    private sealed class ProductListItem
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public int Stock { get; set; }
    }
}
