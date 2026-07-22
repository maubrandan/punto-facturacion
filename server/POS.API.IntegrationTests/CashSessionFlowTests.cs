using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class CashSessionFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CashSessionFlowTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenSaleClose_CashPayment_ComputesExpectedAndZeroDifference()
    {
        var tenant = $"t-cash-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        var open = await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        var productId = await CreateProductAsync(client, "SKU-CASH-1");

        var saleReq = new
        {
            lines = new[] { new { productId, quantity = 1 } },
            payments = new[] { new { method = 0, amount = 121m } }
        };
        var saleRes = await client.PostAsJsonAsync("/api/sales", saleReq);
        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);
        var saleBody = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleData>>();
        Assert.NotNull(saleBody);
        Assert.True(saleBody!.Success);
        var totalVenta = saleBody.Data!.TotalAmount;
        Assert.Equal(121m, totalVenta);

        var sumRes = await client.GetAsync("/api/cash/summary");
        Assert.Equal(HttpStatusCode.OK, sumRes.StatusCode);
        var sum = await sumRes.Content.ReadFromJsonAsync<ApiResponse<SummaryData>>();
        Assert.NotNull(sum?.Data);
        Assert.Equal(100m, sum!.Data!.InitialAmount);
        Assert.Equal(totalVenta, sum.Data.TotalSales);
        Assert.Equal(totalVenta, sum.Data.TotalCashPayments);
        Assert.Equal(0m, sum.Data.TotalCardPayments);
        var esperado = 100m + totalVenta;

        var close = await client.PostAsJsonAsync("/api/cash/close", new { countedAmount = esperado });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var closeBody = await close.Content.ReadFromJsonAsync<ApiResponse<CloseData>>();
        Assert.NotNull(closeBody);
        Assert.True(closeBody!.Success);
        Assert.Equal(esperado, closeBody.Data!.ExpectedAmount);
        Assert.Equal(0m, closeBody.Data.Difference);
    }

    [Fact]
    public async Task CardSale_DoesNotIncreaseProjectedCash()
    {
        var tenant = $"t-card-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        var open = await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 50m });
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        var productId = await CreateProductAsync(client, "SKU-CARD-1");
        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 1, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);

        var sumRes = await client.GetAsync("/api/cash/summary");
        var sum = await sumRes.Content.ReadFromJsonAsync<ApiResponse<SummaryData>>();
        Assert.Equal(121m, sum!.Data!.TotalSales);
        Assert.Equal(0m, sum.Data.TotalCashPayments);
        Assert.Equal(121m, sum.Data.TotalCardPayments);
        Assert.Equal(50m, sum.Data.ProjectedAmount);
    }

    [Fact]
    public async Task SplitPayment_OnlyCashGoesToDrawer()
    {
        var tenant = $"t-split-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        var productId = await CreateProductAsync(client, "SKU-SPLIT-1");

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[]
                {
                    new { method = 0, amount = 40m },
                    new { method = 1, amount = 81m }
                }
            });
        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);

        var sum = await (await client.GetAsync("/api/cash/summary"))
            .Content.ReadFromJsonAsync<ApiResponse<SummaryData>>();
        Assert.Equal(40m, sum!.Data!.TotalCashPayments);
        Assert.Equal(81m, sum.Data.TotalCardPayments);
        Assert.Equal(50m, sum.Data.ProjectedAmount);
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, string sku)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Prod Caja",
                sku,
                barcode = "7791234567890",
                netPrice = 100m,
                taxRate = 21m,
                stock = 5,
                extendedDataJson = new { }
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var body = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProdId>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        return body.Data!.Id;
    }

    private sealed class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }

    private sealed class ProdId
    {
        public Guid Id { get; set; }
    }

    private sealed class SaleData
    {
        public decimal TotalAmount { get; set; }
    }

    private sealed class SummaryData
    {
        public Guid? SessionId { get; set; }
        public decimal? InitialAmount { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCashPayments { get; set; }
        public decimal TotalCardPayments { get; set; }
        public decimal ProjectedAmount { get; set; }
    }

    private sealed class CloseData
    {
        public decimal ExpectedAmount { get; set; }
        public decimal CountedAmount { get; set; }
        public decimal Difference { get; set; }
    }
}
