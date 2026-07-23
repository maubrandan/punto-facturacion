using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class SalesReportIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SalesReportIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Report_BreaksDownByPaymentMethodAndCashier()
    {
        var tenant = $"t-report-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Admin");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "cashier-a");

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        var productId = await CreateProductAsync(client, "SKU-REP-1");

        var saleA = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 0, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.Created, saleA.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "cashier-b");

        var saleB = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[]
                {
                    new { method = 1, amount = 60m },
                    new { method = 2, amount = 61m }
                }
            });
        Assert.Equal(HttpStatusCode.Created, saleB.StatusCode);

        var reportRes = await client.GetAsync("/api/sales/report");
        Assert.Equal(HttpStatusCode.OK, reportRes.StatusCode);
        var report = await reportRes.Content.ReadFromJsonAsync<ApiResponse<SalesReportData>>();
        Assert.NotNull(report?.Data);
        Assert.True(report!.Success);
        Assert.Equal(2, report.Data!.SalesCount);
        Assert.Equal(242m, report.Data.TotalSalesAmount);

        var byMethod = report.Data.ByPaymentMethod.ToDictionary(x => x.Method);
        Assert.Equal(121m, byMethod[0].Amount);
        Assert.Equal(60m, byMethod[1].Amount);
        Assert.Equal(61m, byMethod[2].Amount);

        Assert.Equal(2, report.Data.ByCashier.Count);
        Assert.Contains(report.Data.ByCashier, c => c.CreatedByUserId == "cashier-a" && c.TotalAmount == 121m);
        Assert.Contains(report.Data.ByCashier, c => c.CreatedByUserId == "cashier-b" && c.TotalAmount == 121m);
    }

    [Fact]
    public async Task Report_IsIsolatedByTenant()
    {
        var tenantA = $"t-rep-a-{Guid.NewGuid():N}";
        var tenantB = $"t-rep-b-{Guid.NewGuid():N}";

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Test-TenantId", tenantA);
        clientA.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Admin");
        await clientA.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        var productA = await CreateProductAsync(clientA, "SKU-TA");
        var saleA = await clientA.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId = productA, quantity = 1 } },
                payments = new[] { new { method = 0, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.Created, saleA.StatusCode);

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Test-TenantId", tenantB);
        clientB.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Admin");
        await clientB.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        var productB = await CreateProductAsync(clientB, "SKU-TB");
        var saleB = await clientB.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId = productB, quantity = 1 } },
                payments = new[] { new { method = 1, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.Created, saleB.StatusCode);

        var reportA = await (await clientA.GetAsync("/api/sales/report"))
            .Content.ReadFromJsonAsync<ApiResponse<SalesReportData>>();
        var reportB = await (await clientB.GetAsync("/api/sales/report"))
            .Content.ReadFromJsonAsync<ApiResponse<SalesReportData>>();

        Assert.Equal(1, reportA!.Data!.SalesCount);
        Assert.Equal(121m, reportA.Data.TotalSalesAmount);
        Assert.Single(reportA.Data.ByPaymentMethod);
        Assert.Equal(0, reportA.Data.ByPaymentMethod[0].Method);

        Assert.Equal(1, reportB!.Data!.SalesCount);
        Assert.Equal(121m, reportB.Data.TotalSalesAmount);
        Assert.Single(reportB.Data.ByPaymentMethod);
        Assert.Equal(1, reportB.Data.ByPaymentMethod[0].Method);
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, string sku)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Prod Report",
                sku,
                barcode = "7791234567890",
                netPrice = 100m,
                taxRate = 21m,
                stock = 20,
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
    }

    private sealed class ProdId
    {
        public Guid Id { get; set; }
    }

    private sealed class SalesReportData
    {
        public decimal TotalSalesAmount { get; set; }
        public int SalesCount { get; set; }
        public List<PaymentBreakdown> ByPaymentMethod { get; set; } = new();
        public List<CashierBreakdown> ByCashier { get; set; } = new();
    }

    private sealed class PaymentBreakdown
    {
        public int Method { get; set; }
        public decimal Amount { get; set; }
        public int PaymentCount { get; set; }
    }

    private sealed class CashierBreakdown
    {
        public string? CreatedByUserId { get; set; }
        public string CreatedByUserName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int SalesCount { get; set; }
    }
}
