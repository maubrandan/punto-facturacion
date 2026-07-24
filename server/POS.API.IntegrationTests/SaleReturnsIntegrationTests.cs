using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Domain.Tenant;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class SaleReturnsIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task FullReturn_RestoresStock_AndIsIdempotent()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-ret-stock-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        var productId = await CreateProductAsync(client, stock: 5, netPrice: 100m);
        var saleId = await CreateCashSaleAsync(client, productId, quantity: 2, total: 242m);

        var before = await GetProductStockAsync(client, productId);
        Assert.Equal(3m, before);

        var returnRes = await client.PostAsJsonAsync($"/api/sales/{saleId}/returns", new { });
        Assert.Equal(HttpStatusCode.Created, returnRes.StatusCode);
        var returnBody = await returnRes.Content.ReadFromJsonAsync<ApiResponse<SaleReturnDto>>(JsonOpts);
        Assert.True(returnBody!.Success);
        Assert.Equal(saleId, returnBody.Data!.SaleId);
        Assert.Equal(242m, returnBody.Data.TotalAmount);

        var after = await GetProductStockAsync(client, productId);
        Assert.Equal(5m, after);

        var detailRes = await client.GetAsync($"/api/sales/{saleId}");
        var detail = await detailRes.Content.ReadFromJsonAsync<ApiResponse<SaleDetailDto>>(JsonOpts);
        Assert.Equal(1, detail!.Data!.ReturnStatus);

        var second = await client.PostAsJsonAsync($"/api/sales/{saleId}/returns", new { });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var err = await second.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("sale.already_returned", err!.Error?.Code);
    }

    [Fact]
    public async Task FullReturn_CreditSale_ReversesCustomerBalance()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-ret-cc-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 50m });
        var productId = await CreateProductAsync(client, stock: 5, netPrice: 100m);
        var customerId = await CreateCustomerAsync(client);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                customerId,
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 3, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);
        var sale = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleDto>>(JsonOpts);
        Assert.True(sale!.Success);

        var debt = await client.GetAsync($"/api/customers/{customerId}/account");
        var debtBody = await debt.Content.ReadFromJsonAsync<ApiResponse<AccountDto>>(JsonOpts);
        Assert.Equal(121m, debtBody!.Data!.Balance);

        var returnRes = await client.PostAsJsonAsync($"/api/sales/{sale.Data!.Id}/returns", new { });
        Assert.Equal(HttpStatusCode.Created, returnRes.StatusCode);

        var after = await client.GetAsync($"/api/customers/{customerId}/account");
        var afterBody = await after.Content.ReadFromJsonAsync<ApiResponse<AccountDto>>(JsonOpts);
        Assert.Equal(0m, afterBody!.Data!.Balance);
    }

    [Fact]
    public async Task FullReturn_WithAuthorizedInvoice_IssuesCreditNote()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-ret-nc-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);
        await SeedFiscalProfileAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        var productId = await CreateProductAsync(client, stock: 5, netPrice: 100m);
        var saleId = await CreateCashSaleAsync(client, productId, quantity: 1, total: 121m);

        var issueRes = await client.PostAsJsonAsync(
            "/api/fiscal-documents/issue",
            new { saleId, isInvoiceA = true, buyerTaxId = "30712345678", buyerName = "Cliente Test SA" });
        Assert.Equal(HttpStatusCode.OK, issueRes.StatusCode);
        var issueBody = await issueRes.Content.ReadFromJsonAsync<ApiResponse<FiscalDocDto>>(JsonOpts);
        Assert.True(issueBody!.Success);
        Assert.Equal(2, issueBody.Data!.Status);

        var returnRes = await client.PostAsJsonAsync($"/api/sales/{saleId}/returns", new { });
        Assert.Equal(HttpStatusCode.Created, returnRes.StatusCode);
        var returnBody = await returnRes.Content.ReadFromJsonAsync<ApiResponse<SaleReturnDto>>(JsonOpts);
        Assert.NotNull(returnBody!.Data!.FiscalDocumentId);

        var detailRes = await client.GetAsync($"/api/sales/{saleId}");
        var detail = await detailRes.Content.ReadFromJsonAsync<ApiResponse<SaleDetailDto>>(JsonOpts);
        Assert.Contains(detail!.Data!.FiscalDocuments!, d => d.DocumentType is 3 or 4 && d.Status == 2);
    }

    [Fact]
    public async Task FullReturn_WithoutInvoice_SucceedsWithoutNc()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-ret-nonc-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        var productId = await CreateProductAsync(client, stock: 5, netPrice: 100m);
        var saleId = await CreateCashSaleAsync(client, productId, quantity: 1, total: 121m);

        var returnRes = await client.PostAsJsonAsync($"/api/sales/{saleId}/returns", new { });
        Assert.Equal(HttpStatusCode.Created, returnRes.StatusCode);
        var returnBody = await returnRes.Content.ReadFromJsonAsync<ApiResponse<SaleReturnDto>>(JsonOpts);
        Assert.Null(returnBody!.Data!.FiscalDocumentId);
    }

    [Fact]
    public async Task FullReturn_Cash_ReducesCashSummary()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-ret-cash-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        var productId = await CreateProductAsync(client, stock: 5, netPrice: 100m);
        var saleId = await CreateCashSaleAsync(client, productId, quantity: 1, total: 121m);

        var mid = await client.GetAsync("/api/cash/summary");
        var midBody = await mid.Content.ReadFromJsonAsync<ApiResponse<CashSummaryDto>>(JsonOpts);
        Assert.Equal(221m, midBody!.Data!.ProjectedAmount);
        Assert.Equal(121m, midBody.Data.TotalCashPayments);

        var returnRes = await client.PostAsJsonAsync($"/api/sales/{saleId}/returns", new { });
        Assert.Equal(HttpStatusCode.Created, returnRes.StatusCode);

        var after = await client.GetAsync("/api/cash/summary");
        var afterBody = await after.Content.ReadFromJsonAsync<ApiResponse<CashSummaryDto>>(JsonOpts);
        Assert.Equal(100m, afterBody!.Data!.ProjectedAmount);
        Assert.Equal(0m, afterBody.Data.TotalCashPayments);
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

    private static async Task SeedFiscalProfileAsync(TestWebApplicationFactory factory, string tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<TenantFiscalProfile>().Add(
            new TenantFiscalProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TaxId = "30712345678",
                PointOfSale = 1,
                IsEnabled = true,
                CertificateRef = "dev-cert-ref",
                PrivateKeyRef = "dev-key-ref",
                IsProduction = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, decimal stock, decimal netPrice)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Prod Return",
                sku = $"SKU-R-{Guid.NewGuid():N}"[..12],
                barcode = "7799999888666",
                netPrice,
                taxRate = 21m,
                stock,
                extendedDataJson = new { }
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var body = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProdId>>(JsonOpts);
        return body!.Data!.Id;
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync(
            "/api/customers",
            new { name = "Cliente Ret", taxId = "20111222334", email = "", phone = "", address = "" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<ProdId>>(JsonOpts);
        return body!.Data!.Id;
    }

    private static async Task<Guid> CreateCashSaleAsync(
        HttpClient client,
        Guid productId,
        int quantity,
        decimal total)
    {
        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity } },
                payments = new[] { new { method = 0, amount = total } }
            });
        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);
        var body = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleDto>>(JsonOpts);
        return body!.Data!.Id;
    }

    private static async Task<decimal> GetProductStockAsync(HttpClient client, Guid productId)
    {
        var res = await client.GetAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>(JsonOpts);
        return body!.Data!.Stock;
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

    private sealed class SaleDto
    {
        public Guid Id { get; set; }
        public Guid? CustomerId { get; set; }
    }

    private sealed class SaleReturnDto
    {
        public Guid Id { get; set; }
        public Guid SaleId { get; set; }
        public decimal TotalAmount { get; set; }
        public Guid? FiscalDocumentId { get; set; }
    }

    private sealed class SaleDetailDto
    {
        public int ReturnStatus { get; set; }
        public List<FiscalDocDto>? FiscalDocuments { get; set; }
    }

    private sealed class FiscalDocDto
    {
        public Guid Id { get; set; }
        public int DocumentType { get; set; }
        public int Status { get; set; }
    }

    private sealed class AccountDto
    {
        public decimal Balance { get; set; }
    }

    private sealed class CashSummaryDto
    {
        public decimal ProjectedAmount { get; set; }
        public decimal TotalCashPayments { get; set; }
    }

    private sealed class ProductDto
    {
        public decimal Stock { get; set; }
    }
}
