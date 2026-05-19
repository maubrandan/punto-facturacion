using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class FiscalDocumentsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public FiscalDocumentsIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task IssueAndCreditNote_ReturnAuthorizedDocuments()
    {
        var tenant = $"t-fiscal-{Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenant);

        await SeedFiscalProfileAsync(tenant);
        var saleId = await CreateSaleAsync(client);

        var issueRes = await client.PostAsJsonAsync(
            "/api/fiscal-documents/issue",
            new { saleId, isInvoiceA = true });
        Assert.Equal(HttpStatusCode.OK, issueRes.StatusCode);
        var issueBody = await issueRes.Content.ReadFromJsonAsync<ApiResponse<FiscalDocumentData>>();
        Assert.NotNull(issueBody);
        Assert.True(issueBody!.Success);
        Assert.NotNull(issueBody.Data?.Cae);
        Assert.Equal(2, issueBody.Data!.Status);

        var creditRes = await client.PostAsJsonAsync(
            "/api/fiscal-documents/credit-note",
            new
            {
                originalFiscalDocumentId = issueBody.Data.Id,
                saleId,
                amount = 50m
            });
        Assert.Equal(HttpStatusCode.OK, creditRes.StatusCode);
        var creditBody = await creditRes.Content.ReadFromJsonAsync<ApiResponse<FiscalDocumentData>>();
        Assert.NotNull(creditBody);
        Assert.True(creditBody!.Success);
        Assert.Equal(3, creditBody.Data!.DocumentType);
        Assert.Equal(2, creditBody.Data.Status);
    }

    private async Task SeedFiscalProfileAsync(string tenantId)
    {
        using var scope = _factory.Services.CreateScope();
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

    private static async Task<Guid> CreateSaleAsync(HttpClient client)
    {
        var open = await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        var createProduct = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Prod Fiscal",
                sku = $"SKU-FISC-{Guid.NewGuid():N}".Substring(0, 14),
                barcode = "7791234567890",
                netPrice = 100m,
                taxRate = 21m,
                stock = 10,
                extendedDataJson = new { }
            });
        Assert.Equal(HttpStatusCode.Created, createProduct.StatusCode);
        var productBody = await createProduct.Content.ReadFromJsonAsync<ApiResponse<ProdId>>();
        Assert.NotNull(productBody?.Data);

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId = productBody!.Data!.Id, quantity = 1 } }
            });
        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);
        var saleBody = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleData>>();
        Assert.NotNull(saleBody?.Data);
        return saleBody!.Data!.Id;
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
        public Guid Id { get; set; }
    }

    private sealed class FiscalDocumentData
    {
        public Guid Id { get; set; }
        public int DocumentType { get; set; }
        public int Status { get; set; }
        public string? Cae { get; set; }
    }
}
