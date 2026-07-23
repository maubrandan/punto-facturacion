using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class InventoryP2IntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InventoryP2IntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdjustmentReasons_ReturnsSharedCatalog()
    {
        var tenantId = $"t-reasons-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Kiosco);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var response = await client.GetAsync("/api/inventory/adjustment-reasons");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ReasonOption>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Contains(body.Data!, r => r.Code == StockAdjustmentReasonCodes.CountCorrection);
        Assert.Contains(body.Data!, r => r.Code == StockAdjustmentReasonCodes.ExpiredDisposal);
        Assert.All(body.Data!, r => Assert.False(string.IsNullOrWhiteSpace(r.Label)));
    }

    [Fact]
    public async Task Adjust_RequiresTypedReasonCode()
    {
        var tenantId = $"t-adj-reason-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Kiosco);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var productId = await CreateProductAsync(client, "SKU-ADJ-1", stock: 5);

        var bad = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new { productId, quantityDelta = 1, reason = "texto libre" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var badBody = await bad.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("stock.reason_invalid", badBody!.Error?.Code);

        var ok = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new
            {
                productId,
                quantityDelta = 1,
                reasonCode = StockAdjustmentReasonCodes.Damage,
                note = "Caja rota"
            });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var okBody = await ok.Content.ReadFromJsonAsync<ApiResponse<AdjustData>>();
        Assert.True(okBody!.Success);
        Assert.Equal(StockAdjustmentReasonCodes.Damage, okBody.Data!.ReasonCode);
        Assert.Equal(6m, okBody.Data.StockAfter);

        var movements = await client.GetAsync("/api/inventory/movements?pageSize=5");
        var movBody = await movements.Content.ReadFromJsonAsync<ApiResponse<PagedMovements>>();
        Assert.True(movBody!.Success);
        var adjustment = Assert.Single(
            movBody.Data!.Items,
            m => m.Type == nameof(StockMovementType.Adjustment));
        Assert.Equal(StockAdjustmentReasonCodes.Damage, adjustment.ReasonCode);
        Assert.Equal("Caja rota", adjustment.ReasonNote);
    }

    [Fact]
    public async Task Movements_FilterByPeriod()
    {
        var tenantId = $"t-mov-period-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Kiosco);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var productId = await CreateProductAsync(client, "SKU-MOV-1", stock: 1);

        var adjust = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new
            {
                productId,
                quantityDelta = 2,
                reasonCode = StockAdjustmentReasonCodes.CountCorrection
            });
        Assert.Equal(HttpStatusCode.OK, adjust.StatusCode);

        var from = DateTime.UtcNow.AddMinutes(-5).ToString("o");
        var to = DateTime.UtcNow.AddMinutes(5).ToString("o");
        var inRange = await client.GetAsync($"/api/inventory/movements?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        var inBody = await inRange.Content.ReadFromJsonAsync<ApiResponse<PagedMovements>>();
        Assert.True(inBody!.Success);
        Assert.True(inBody.Data!.TotalCount >= 1);

        var pastTo = DateTime.UtcNow.AddDays(-2).ToString("o");
        var empty = await client.GetAsync($"/api/inventory/movements?to={Uri.EscapeDataString(pastTo)}");
        var emptyBody = await empty.Content.ReadFromJsonAsync<ApiResponse<PagedMovements>>();
        Assert.True(emptyBody!.Success);
        Assert.Equal(0, emptyBody.Data!.TotalCount);
    }

    [Fact]
    public async Task ExpiryAlerts_Farmacia_ReturnsExpiredAndExpiringSoon()
    {
        var tenantId = $"t-exp-farm-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Farmacia);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CreateProductAsync(
            client,
            "SKU-EXP-1",
            stock: 2,
            lotNumber: "EXPIRED",
            expirationDate: today.AddDays(-3).ToString("yyyy-MM-dd"));

        var productId = await CreateProductAsync(
            client,
            "SKU-EXP-2",
            stock: 1,
            lotNumber: "SOON",
            expirationDate: today.AddDays(10).ToString("yyyy-MM-dd"));

        var far = await client.PostAsJsonAsync(
            "/api/inventory/adjustments",
            new
            {
                productId,
                quantityDelta = 1,
                reasonCode = StockAdjustmentReasonCodes.CountCorrection,
                lotNumber = "FAR",
                expirationDate = today.AddDays(120).ToString("yyyy-MM-dd")
            });
        Assert.Equal(HttpStatusCode.OK, far.StatusCode);

        var response = await client.GetAsync("/api/inventory/expiry-alerts?withinDays=30");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ExpiryAlerts>>();
        Assert.True(body!.Success);
        Assert.True(body.Data!.Supported);
        Assert.Equal(30, body.Data.WithinDays);
        Assert.Equal(2, body.Data.Items.Count);
        Assert.Contains(body.Data.Items, i => i.Status == ExpiryAlertStatuses.Expired && i.LotNumber == "EXPIRED");
        Assert.Contains(body.Data.Items, i => i.Status == ExpiryAlertStatuses.ExpiringSoon && i.LotNumber == "SOON");
        Assert.DoesNotContain(body.Data.Items, i => i.LotNumber == "FAR");
    }

    [Fact]
    public async Task ExpiryAlerts_Kiosco_ReturnsUnsupportedEmpty()
    {
        var tenantId = $"t-exp-kiosk-{Guid.NewGuid():N}";
        await EnsureTenantAsync(tenantId, BusinessTypeNames.Kiosco);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await CreateProductAsync(client, "SKU-EXP-K", stock: 3);

        var response = await client.GetAsync("/api/inventory/expiry-alerts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ExpiryAlerts>>();
        Assert.True(body!.Success);
        Assert.False(body.Data!.Supported);
        Assert.Empty(body.Data.Items);
    }

    [Fact]
    public async Task ExpiryAlerts_AreTenantIsolated()
    {
        var farmA = $"t-exp-a-{Guid.NewGuid():N}";
        var farmB = $"t-exp-b-{Guid.NewGuid():N}";
        await EnsureTenantAsync(farmA, BusinessTypeNames.Farmacia);
        await EnsureTenantAsync(farmB, BusinessTypeNames.Farmacia);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Test-TenantId", farmA);
        await CreateProductAsync(
            clientA,
            "SKU-A",
            stock: 1,
            lotNumber: "A-LOT",
            expirationDate: today.AddDays(5).ToString("yyyy-MM-dd"));

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Test-TenantId", farmB);
        await CreateProductAsync(
            clientB,
            "SKU-B",
            stock: 1,
            lotNumber: "B-LOT",
            expirationDate: today.AddDays(5).ToString("yyyy-MM-dd"));

        var resA = await clientA.GetAsync("/api/inventory/expiry-alerts?withinDays=30");
        var bodyA = await resA.Content.ReadFromJsonAsync<ApiResponse<ExpiryAlerts>>();
        Assert.True(bodyA!.Success);
        Assert.All(bodyA.Data!.Items, i => Assert.Equal("A-LOT", i.LotNumber));
        Assert.DoesNotContain(bodyA.Data.Items, i => i.LotNumber == "B-LOT");
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
            ["barcode"] = $"779{Math.Abs(sku.GetHashCode()):X}",
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

    private sealed class ReasonOption
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    private sealed class AdjustData
    {
        public decimal StockAfter { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
    }

    private sealed class PagedMovements
    {
        public List<MovementItem> Items { get; set; } = [];
        public int TotalCount { get; set; }
    }

    private sealed class MovementItem
    {
        public string Type { get; set; } = string.Empty;
        public string? ReasonCode { get; set; }
        public string? ReasonNote { get; set; }
    }

    private sealed class ExpiryAlerts
    {
        public bool Supported { get; set; }
        public int WithinDays { get; set; }
        public List<ExpiryItem> Items { get; set; } = [];
    }

    private sealed class ExpiryItem
    {
        public string LotNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
