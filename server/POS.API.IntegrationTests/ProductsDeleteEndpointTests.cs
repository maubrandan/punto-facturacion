using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class ProductsDeleteEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ProductsDeleteEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Delete_WhenProductExistsForTenant_ReturnsSuccess()
    {
        using var client = _factory.CreateClient();
        var productId = await CreateProductAsync(client, "tenant-a", "SKU-DEL-001");

        var response = await client.DeleteAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Null(body.Error);
    }

    [Fact]
    public async Task Delete_WhenProductDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("product.not_found", body.Error?.Code);
    }

    [Fact]
    public async Task Delete_WhenProductBelongsToAnotherTenant_ReturnsNotFound()
    {
        using var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Add("X-Test-TenantId", "tenant-owner");
        var productId = await CreateProductAsync(ownerClient, "tenant-owner", "SKU-DEL-002");

        using var otherTenantClient = _factory.CreateClient();
        otherTenantClient.DefaultRequestHeaders.Add("X-Test-TenantId", "tenant-other");
        var deleteResponse = await otherTenantClient.DeleteAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var verifyOwnerCanStillRead = await ownerClient.GetAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, verifyOwnerCanStillRead.StatusCode);
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, string tenantId, string sku)
    {
        if (!client.DefaultRequestHeaders.Contains("X-Test-TenantId"))
            client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Producto Test",
                sku,
                barcode = "7791234567890",
                netPrice = 100m,
                taxRate = 21m,
                stock = 5,
                extendedDataJson = new { flavor = "cola", size = "500ml" }
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
}
