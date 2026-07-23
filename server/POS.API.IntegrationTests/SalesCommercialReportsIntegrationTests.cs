using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class SalesCommercialReportsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SalesCommercialReportsIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MarginReport_UsesLastCost_AndFlagsLinesWithoutCost()
    {
        var tenant = $"t-margin-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Admin");

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });

        var productWithCost = await CreateProductAsync(client, "SKU-M-COST", netPrice: 100m);
        var productNoCost = await CreateProductAsync(client, "SKU-M-NO", netPrice: 50m);
        await PurchaseAsync(client, productWithCost, unitCost: 40m, quantity: 10);

        var sale = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[]
                {
                    new { productId = productWithCost, quantity = 2 },
                    new { productId = productNoCost, quantity = 1 }
                },
                payments = new[] { new { method = 0, amount = 302.5m } }
            });
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);

        var res = await client.GetAsync("/api/sales/report/margin");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<MarginReportData>>();
        Assert.NotNull(body?.Data);
        Assert.True(body!.Success);

        // net: 2*100 + 1*50 = 250
        Assert.Equal(250m, body.Data!.RevenueNet);
        Assert.Equal(200m, body.Data.RevenueNetWithCost);
        Assert.Equal(50m, body.Data.RevenueNetWithoutCost);
        Assert.Equal(80m, body.Data.CostNet); // 2 * 40
        Assert.Equal(120m, body.Data.MarginNet); // 200 - 80
        Assert.Equal(1, body.Data.LinesWithCost);
        Assert.Equal(1, body.Data.LinesWithoutCost);
        Assert.Contains(body.Data.BySku, x => x.ProductId == productWithCost && x.HasCost && x.MarginNet == 120m);
        Assert.Contains(body.Data.BySku, x => x.ProductId == productNoCost && !x.HasCost);
    }

    [Fact]
    public async Task TopSkus_OrdersByQuantityAndRevenue()
    {
        var tenant = $"t-topsku-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Admin");

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 50m });
        var cheap = await CreateProductAsync(client, "SKU-CHEAP", netPrice: 10m);
        var pricey = await CreateProductAsync(client, "SKU-PRICY", netPrice: 100m);

        var sale = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[]
                {
                    new { productId = cheap, quantity = 5 },
                    new { productId = pricey, quantity = 1 }
                },
                // 5*12.1 + 1*121 = 60.5 + 121 = 181.5
                payments = new[] { new { method = 0, amount = 181.5m } }
            });
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);

        var byQty = await (await client.GetAsync("/api/sales/report/top-skus?sortBy=quantity&take=10"))
            .Content.ReadFromJsonAsync<ApiResponse<TopSkusReportData>>();
        Assert.True(byQty!.Success);
        Assert.Equal(2, byQty.Data!.Items.Count);
        Assert.Equal(cheap, byQty.Data.Items[0].ProductId);
        Assert.Equal(5m, byQty.Data.Items[0].Quantity);

        var byRev = await (await client.GetAsync("/api/sales/report/top-skus?sortBy=revenue&take=10"))
            .Content.ReadFromJsonAsync<ApiResponse<TopSkusReportData>>();
        Assert.True(byRev!.Success);
        Assert.Equal(pricey, byRev.Data!.Items[0].ProductId);
        Assert.Equal(100m, byRev.Data.Items[0].RevenueNet);
    }

    [Fact]
    public async Task ByPeriod_DayBuckets_AndInvalidPeriodRejected()
    {
        var tenant = $"t-period-{Guid.NewGuid():N}";
        using var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);
        admin.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Admin");

        await admin.PostAsJsonAsync("/api/cash/open", new { initialAmount = 20m });
        var productId = await CreateProductAsync(admin, "SKU-PER");

        var sale = await admin.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 0, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);

        using var cashier = _factory.CreateClient();
        cashier.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);
        cashier.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Cashier");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var res = await cashier.GetAsync($"/api/sales/report/by-period?startDate={today}&endDate={today}&period=day");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<ByPeriodReportData>>();
        Assert.True(body!.Success);
        Assert.Equal(1, body.Data!.SalesCount);
        Assert.Equal(121m, body.Data.TotalSalesAmount);
        Assert.Single(body.Data.Buckets);
        Assert.Equal(121m, body.Data.Buckets[0].TotalSalesAmount);

        var bad = await cashier.GetAsync("/api/sales/report/by-period?period=year");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var badBody = await bad.Content.ReadFromJsonAsync<ApiResponse<ByPeriodReportData>>();
        Assert.False(badBody!.Success);
    }

    [Fact]
    public async Task CommercialReports_AreIsolatedByTenant()
    {
        var tenantA = $"t-com-a-{Guid.NewGuid():N}";
        var tenantB = $"t-com-b-{Guid.NewGuid():N}";

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Test-TenantId", tenantA);
        clientA.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Admin");
        await clientA.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        var productA = await CreateProductAsync(clientA, "SKU-CA");
        Assert.Equal(
            HttpStatusCode.Created,
            (await clientA.PostAsJsonAsync(
                "/api/sales",
                new
                {
                    lines = new[] { new { productId = productA, quantity = 1 } },
                    payments = new[] { new { method = 0, amount = 121m } }
                })).StatusCode);

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Test-TenantId", tenantB);
        clientB.DefaultRequestHeaders.Add("X-Test-Roles", "Tenant.Admin");
        await clientB.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        var productB = await CreateProductAsync(clientB, "SKU-CB");
        Assert.Equal(
            HttpStatusCode.Created,
            (await clientB.PostAsJsonAsync(
                "/api/sales",
                new
                {
                    lines = new[] { new { productId = productB, quantity = 2 } },
                    payments = new[] { new { method = 0, amount = 242m } }
                })).StatusCode);

        var topA = await (await clientA.GetAsync("/api/sales/report/top-skus?sortBy=quantity"))
            .Content.ReadFromJsonAsync<ApiResponse<TopSkusReportData>>();
        var topB = await (await clientB.GetAsync("/api/sales/report/top-skus?sortBy=quantity"))
            .Content.ReadFromJsonAsync<ApiResponse<TopSkusReportData>>();

        Assert.Single(topA!.Data!.Items);
        Assert.Equal(productA, topA.Data.Items[0].ProductId);
        Assert.Equal(1m, topA.Data.Items[0].Quantity);

        Assert.Single(topB!.Data!.Items);
        Assert.Equal(productB, topB.Data.Items[0].ProductId);
        Assert.Equal(2m, topB.Data.Items[0].Quantity);
    }

    private static async Task PurchaseAsync(HttpClient client, Guid productId, decimal unitCost, decimal quantity)
    {
        var providerRes = await client.PostAsJsonAsync(
            "/api/providers",
            new
            {
                name = "Prov Report",
                taxId = "20123456789",
                email = "prov@test.local",
                phone = "111"
            });
        Assert.Equal(HttpStatusCode.Created, providerRes.StatusCode);
        var provider = await providerRes.Content.ReadFromJsonAsync<ApiResponse<IdBody>>();
        Assert.NotNull(provider?.Data);

        var purchaseRes = await client.PostAsJsonAsync(
            "/api/purchases",
            new
            {
                providerId = provider!.Data!.Id,
                date = DateTime.UtcNow,
                invoiceNumber = $"FAC-{Guid.NewGuid():N}"[..12],
                lines = new[]
                {
                    new
                    {
                        productId,
                        quantity,
                        unitCost,
                        lotNumber = (string?)null,
                        expirationDate = (DateOnly?)null
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, purchaseRes.StatusCode);
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, string sku, decimal netPrice = 100m)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = $"Prod {sku}",
                sku,
                barcode = "7791234567890",
                netPrice,
                taxRate = 21m,
                stock = 50,
                extendedDataJson = new { }
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var body = await createResponse.Content.ReadFromJsonAsync<ApiResponse<IdBody>>();
        return body!.Data!.Id;
    }

    private sealed class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }

    private sealed class IdBody
    {
        public Guid Id { get; set; }
    }

    private sealed class MarginReportData
    {
        public decimal RevenueNet { get; set; }
        public decimal RevenueNetWithCost { get; set; }
        public decimal RevenueNetWithoutCost { get; set; }
        public decimal CostNet { get; set; }
        public decimal MarginNet { get; set; }
        public int LinesWithCost { get; set; }
        public int LinesWithoutCost { get; set; }
        public List<MarginSkuItem> BySku { get; set; } = new();
    }

    private sealed class MarginSkuItem
    {
        public Guid ProductId { get; set; }
        public bool HasCost { get; set; }
        public decimal? MarginNet { get; set; }
    }

    private sealed class TopSkusReportData
    {
        public List<TopSkuItem> Items { get; set; } = new();
    }

    private sealed class TopSkuItem
    {
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal RevenueNet { get; set; }
    }

    private sealed class ByPeriodReportData
    {
        public decimal TotalSalesAmount { get; set; }
        public int SalesCount { get; set; }
        public List<PeriodBucket> Buckets { get; set; } = new();
    }

    private sealed class PeriodBucket
    {
        public decimal TotalSalesAmount { get; set; }
        public int SalesCount { get; set; }
    }
}
