using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class StockPolicyIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StockPolicyIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Kiosco_SaleWithFractionalQuantity_ReturnsBadRequest()
    {
        var tenantId = $"t-kiosk-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Kiosco);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(client, "SKU-K-1", stock: 10);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 0.5m } },
                payments = new[] { new { method = 0, amount = 1m } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("stock.quantity_whole", body.Error?.Code);
    }

    [Fact]
    public async Task Ferreteria_SaleWithDecimalQuantity_Succeeds()
    {
        var tenantId = $"t-ferr-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Ferreteria);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(client, "SKU-F-1", stock: 10);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1.250m } },
                payments = new[] { new { method = 0, amount = 151.25m } }
            });

        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleData>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal(1.250m, body.Data!.Lines[0].Quantity);
    }

    [Fact]
    public async Task Ferreteria_SaleWithMoreThanThreeDecimals_ReturnsBadRequest()
    {
        var tenantId = $"t-ferr-scale-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Ferreteria);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(client, "SKU-F-SCALE", stock: 10);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1.2345m } },
                payments = new[] { new { method = 0, amount = 121m } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("stock.quantity_scale", body!.Error?.Code);
    }

    [Fact]
    public async Task Farmacia_SaleWithoutLot_AutoAllocatesFefoLot()
    {
        var tenantId = $"t-farm-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Farmacia);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(
            client,
            "SKU-PH-1",
            stock: 5,
            lotNumber: "L1",
            expirationDate: "2099-01-01");

        var lotsRes = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        var lotsBody = await lotsRes.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        var expectedLotId = lotsBody!.Data![0].Id;

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 0, amount = 121m } }
            });

        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleData>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal(expectedLotId, body.Data!.Lines[0].StockLotId);

        var lotsAfter = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        var lotsAfterBody = await lotsAfter.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        Assert.Equal(4m, lotsAfterBody!.Data!.Single(l => l.Id == expectedLotId).Quantity);
    }

    [Fact]
    public async Task Farmacia_SaleWithoutLot_PrefersEarliestExpiringLot()
    {
        var tenantId = $"t-farm-fefo-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Farmacia);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(
            client,
            "SKU-PH-FEFO",
            stock: 2,
            lotNumber: "LATE",
            expirationDate: "2099-12-01");

        var adjustRes = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new
            {
                productId,
                quantityDelta = 3,
                reasonCode = "CountCorrection",
                lotNumber = "EARLY",
                expirationDate = "2099-06-01"
            });
        Assert.Equal(HttpStatusCode.OK, adjustRes.StatusCode);

        var lotsRes = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        var lotsBody = await lotsRes.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        Assert.Equal(2, lotsBody!.Data!.Count);
        var earlyLot = lotsBody.Data.OrderBy(l => l.ExpirationDate).First();
        Assert.Equal("EARLY", earlyLot.LotNumber);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 2 } },
                payments = new[] { new { method = 0, amount = 242m } }
            });

        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleData>>();
        Assert.Equal(earlyLot.Id, body!.Data!.Lines[0].StockLotId);

        var lotsAfter = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        var lotsAfterBody = await lotsAfter.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        var afterLots = lotsAfterBody!.Data!;
        Assert.Equal(1m, afterLots.Single(l => l.Id == earlyLot.Id).Quantity);
        Assert.Equal(2m, afterLots.Single(l => l.LotNumber == "LATE").Quantity);
    }

    [Fact]
    public async Task Farmacia_SaleWithoutLot_SplitsAcrossLotsInFefoOrder()
    {
        var tenantId = $"t-farm-split-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Farmacia);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(
            client,
            "SKU-PH-SPLIT",
            stock: 1,
            lotNumber: "EARLY",
            expirationDate: "2099-06-01");

        var adjustRes = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new
            {
                productId,
                quantityDelta = 10,
                reasonCode = "CountCorrection",
                lotNumber = "LATE",
                expirationDate = "2099-12-01"
            });
        Assert.Equal(HttpStatusCode.OK, adjustRes.StatusCode);

        var lotsRes = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        var lotsBody = await lotsRes.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        Assert.NotNull(lotsBody?.Data);
        var earlyLot = lotsBody!.Data!.Single(l => l.LotNumber == "EARLY");
        var lateLot = lotsBody.Data.Single(l => l.LotNumber == "LATE");

        // 1 del EARLY + 1 del LATE (FEFO), total 2.
        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 2 } },
                payments = new[] { new { method = 0, amount = 242m } }
            });

        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleData>>();
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(2, body.Data!.Lines.Count);
        Assert.Equal(earlyLot.Id, body.Data.Lines[0].StockLotId);
        Assert.Equal("EARLY", body.Data.Lines[0].LotNumber);
        Assert.Equal(1m, body.Data.Lines[0].Quantity);
        Assert.Equal(lateLot.Id, body.Data.Lines[1].StockLotId);
        Assert.Equal("LATE", body.Data.Lines[1].LotNumber);
        Assert.Equal(1m, body.Data.Lines[1].Quantity);

        var detailRes = await client.GetAsync($"/api/sales/{body.Data.Id}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detailBody = await detailRes.Content.ReadFromJsonAsync<ApiResponse<SaleDetailData>>();
        Assert.True(detailBody!.Success);
        Assert.NotNull(detailBody.Data);
        Assert.Equal(2, detailBody.Data!.Lines.Count);
        Assert.Equal(earlyLot.Id, detailBody.Data.Lines[0].StockLotId);
        Assert.Equal("EARLY", detailBody.Data.Lines[0].LotNumber);
        Assert.Equal(lateLot.Id, detailBody.Data.Lines[1].StockLotId);
        Assert.Equal("LATE", detailBody.Data.Lines[1].LotNumber);

        var lotsAfter = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        var lotsAfterBody = await lotsAfter.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        Assert.NotNull(lotsAfterBody?.Data);
        Assert.Equal(0m, lotsAfterBody!.Data!.Single(l => l.Id == earlyLot.Id).Quantity);
        Assert.Equal(9m, lotsAfterBody.Data.Single(l => l.Id == lateLot.Id).Quantity);
    }

    [Fact]
    public async Task Farmacia_SaleWithoutLot_WhenTotalFefoQtyInsufficient_ReturnsBadRequest()
    {
        var tenantId = $"t-farm-short-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Farmacia);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(
            client,
            "SKU-PH-SHORT",
            stock: 1,
            lotNumber: "EARLY",
            expirationDate: "2099-06-01");

        var adjustRes = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new
            {
                productId,
                quantityDelta = 1,
                reasonCode = "CountCorrection",
                lotNumber = "LATE",
                expirationDate = "2099-12-01"
            });
        Assert.Equal(HttpStatusCode.OK, adjustRes.StatusCode);

        // Total FEFO = 2; pedir 3 debe fallar.
        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 3 } },
                payments = new[] { new { method = 0, amount = 363m } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("stock.insufficient", body!.Error?.Code);
        Assert.Contains("FEFO", body.Error!.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Disponible: 2", body.Error.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Farmacia_SaleWithExplicitLot_DoesNotSplitToOtherLots()
    {
        var tenantId = $"t-farm-explicit-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Farmacia);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(
            client,
            "SKU-PH-EXPL",
            stock: 1,
            lotNumber: "EARLY",
            expirationDate: "2099-06-01");

        var adjustRes = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new
            {
                productId,
                quantityDelta = 10,
                reasonCode = "CountCorrection",
                lotNumber = "LATE",
                expirationDate = "2099-12-01"
            });
        Assert.Equal(HttpStatusCode.OK, adjustRes.StatusCode);

        var lotsRes = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        var lotsBody = await lotsRes.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        Assert.NotNull(lotsBody?.Data);
        var earlyLot = lotsBody!.Data!.Single(l => l.LotNumber == "EARLY");

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 2, stockLotId = earlyLot.Id } },
                payments = new[] { new { method = 0, amount = 242m } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("stock.insufficient", body!.Error?.Code);
        Assert.DoesNotContain("FEFO", body.Error!.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EARLY", body.Error.Message!, StringComparison.Ordinal);

        var lotsAfter = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        var lotsAfterBody = await lotsAfter.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        Assert.NotNull(lotsAfterBody?.Data);
        Assert.Equal(1m, lotsAfterBody!.Data!.Single(l => l.LotNumber == "EARLY").Quantity);
        Assert.Equal(10m, lotsAfterBody.Data.Single(l => l.LotNumber == "LATE").Quantity);
    }

    [Fact]
    public async Task Farmacia_SaleWithoutLot_WhenOnlyExpiredLots_ReturnsBadRequest()
    {
        var tenantId = $"t-farm-exp-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Farmacia);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(
            client,
            "SKU-PH-ONLY-EXP",
            stock: 3,
            lotNumber: "EXPIRED",
            expirationDate: "2020-01-01");

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 0, amount = 121m } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("stock.no_fefo_lot", body!.Error?.Code);
    }

    [Fact]
    public async Task Farmacia_SaleWithExpiredLot_ReturnsBadRequest_AndAdjustWithLot_Succeeds()
    {
        var tenantId = $"t-farm2-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Farmacia);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await OpenCashAsync(client);
        var productId = await CreateProductAsync(
            client,
            "SKU-PH-2",
            stock: 3,
            lotNumber: "EXPIRED",
            expirationDate: "2020-01-01");

        var lotsRes = await client.GetAsync($"/api/inventory/products/{productId}/lots");
        Assert.Equal(HttpStatusCode.OK, lotsRes.StatusCode);
        var lotsBody = await lotsRes.Content.ReadFromJsonAsync<ApiResponse<List<LotData>>>();
        Assert.NotNull(lotsBody?.Data);
        Assert.Single(lotsBody!.Data!);
        var lotId = lotsBody.Data[0].Id;
        Assert.True(lotsBody.Data[0].IsExpired);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1, stockLotId = lotId } },
                payments = new[] { new { method = 0, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var saleBody = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("stock.lot_expired", saleBody!.Error?.Code);

        var adjustRes = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new
            {
                productId,
                quantityDelta = 2,
                reasonCode = "CountCorrection",
                lotNumber = "L-NEW",
                expirationDate = "2099-06-01"
            });
        Assert.Equal(HttpStatusCode.OK, adjustRes.StatusCode);
        var adjustBody = await adjustRes.Content.ReadFromJsonAsync<ApiResponse<AdjustData>>();
        Assert.True(adjustBody!.Success);
        Assert.Equal(5m, adjustBody.Data!.StockAfter);
    }

    private async Task EnsureTenantAsync(string tenantId, string businessType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Tenants.FindAsync(tenantId) is null)
        {
            db.Tenants.Add(
                new Tenant
                {
                    Id = tenantId,
                    Name = $"Test {businessType}",
                    BusinessType = businessType,
                    Status = TenantStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }
    }

    private static async Task OpenCashAsync(HttpClient client)
    {
        var open = await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
    }

    private static async Task<Guid> CreateProductAsync(
        HttpClient client,
        string sku,
        decimal stock,
        string? lotNumber = null,
        string? expirationDate = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = $"Prod {sku}",
            ["sku"] = sku,
            ["barcode"] = $"779{sku.GetHashCode():X}",
            ["netPrice"] = 100m,
            ["taxRate"] = 21m,
            ["stock"] = stock,
            ["extendedDataJson"] = new { }
        };
        if (lotNumber is not null)
            payload["lotNumber"] = lotNumber;
        if (expirationDate is not null)
            payload["expirationDate"] = expirationDate;

        var createResponse = await client.PostAsJsonAsync("/api/products", payload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var body = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProdId>>();
        Assert.True(body!.Success);
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

    private sealed class ProdId
    {
        public Guid Id { get; set; }
    }

    private sealed class SaleData
    {
        public Guid Id { get; set; }

        public List<SaleLineData> Lines { get; set; } = [];
    }

    private sealed class SaleLineData
    {
        public decimal Quantity { get; set; }

        public Guid? StockLotId { get; set; }

        public string? LotNumber { get; set; }
    }

    private sealed class SaleDetailData
    {
        public List<SaleDetailLineData> Lines { get; set; } = [];
    }

    private sealed class SaleDetailLineData
    {
        public Guid? StockLotId { get; set; }

        public string? LotNumber { get; set; }

        public decimal Quantity { get; set; }
    }

    private sealed class LotData
    {
        public Guid Id { get; set; }

        public string LotNumber { get; set; } = string.Empty;

        public DateOnly ExpirationDate { get; set; }

        public decimal Quantity { get; set; }

        public bool IsExpired { get; set; }
    }

    private sealed class AdjustData
    {
        public decimal StockAfter { get; set; }
    }
}
