using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class SalePaymentsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SalePaymentsIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SaleWithoutPayments_ReturnsPaymentRequired()
    {
        var tenant = $"t-pay-req-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        var productId = await CreateProductAsync(client);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new { lines = new[] { new { productId, quantity = 1 } }, payments = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("sale.payment_required", body!.Error?.Code);
    }

    [Fact]
    public async Task SaleWithMismatchedPayment_ReturnsMismatch()
    {
        var tenant = $"t-pay-mis-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        var productId = await CreateProductAsync(client);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 0, amount = 100m } }
            });
        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("sale.payment_mismatch", body!.Error?.Code);
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Prod Pay",
                sku = $"SKU-P-{Guid.NewGuid():N}"[..12],
                barcode = "7799999888777",
                netPrice = 100m,
                taxRate = 21m,
                stock = 5,
                extendedDataJson = new { }
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var body = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProdId>>();
        return body!.Data!.Id;
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

    private sealed class ProdId
    {
        public Guid Id { get; set; }
    }
}
