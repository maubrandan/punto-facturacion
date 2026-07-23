using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Domain.Tenant;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class CustomerAccountIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task CreditSale_RequiresCustomer()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-cc-req-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 100m });
        var productId = await CreateProductAsync(client, "SKU-CC-REQ");

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 3, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var err = await saleRes.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("sale.customer_required", err!.Error?.Code);
    }

    [Fact]
    public async Task CreditSale_CreatesDebt_AndDoesNotIncreaseProjectedCash()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-cc-debt-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 50m });
        var productId = await CreateProductAsync(client, "SKU-CC-DEBT");
        var customerId = await CreateCustomerAsync(client, "Cliente CC", "20111222333");

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
        Assert.Equal(customerId, sale.Data!.CustomerId);

        var accountRes = await client.GetAsync($"/api/customers/{customerId}/account");
        Assert.Equal(HttpStatusCode.OK, accountRes.StatusCode);
        var account = await accountRes.Content.ReadFromJsonAsync<ApiResponse<AccountDto>>(JsonOpts);
        Assert.True(account!.Success);
        Assert.Equal(121m, account.Data!.Balance);
        Assert.NotEmpty(account.Data.RecentMovements);
        Assert.Equal(0, account.Data.RecentMovements[0].Type);
        Assert.Equal(121m, account.Data.RecentMovements[0].Amount);

        var sumRes = await client.GetAsync("/api/cash/summary");
        var sum = await sumRes.Content.ReadFromJsonAsync<ApiResponse<SummaryDto>>(JsonOpts);
        Assert.Equal(121m, sum!.Data!.TotalSales);
        Assert.Equal(0m, sum.Data.TotalCashPayments);
        Assert.Equal(121m, sum.Data.TotalCreditPayments);
        Assert.Equal(50m, sum.Data.ProjectedAmount);
    }

    [Fact]
    public async Task DebtPayment_Cash_IncreasesProjectedCash()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = $"t-cc-pay-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 20m });
        var productId = await CreateProductAsync(client, "SKU-CC-PAY");
        var customerId = await CreateCustomerAsync(client, "Deudor", "20999888777");

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                customerId,
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[]
                {
                    new { method = 0, amount = 40m },
                    new { method = 3, amount = 81m }
                }
            });
        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);

        var sumAfterSale = await (await client.GetAsync("/api/cash/summary"))
            .Content.ReadFromJsonAsync<ApiResponse<SummaryDto>>(JsonOpts);
        Assert.Equal(40m, sumAfterSale!.Data!.TotalCashPayments);
        Assert.Equal(81m, sumAfterSale.Data.TotalCreditPayments);
        Assert.Equal(60m, sumAfterSale.Data.ProjectedAmount);

        var payRes = await client.PostAsJsonAsync(
            $"/api/customers/{customerId}/account/payments",
            new { amount = 50m, method = 0, notes = "Pago parcial" });
        Assert.Equal(HttpStatusCode.Created, payRes.StatusCode);
        var pay = await payRes.Content.ReadFromJsonAsync<ApiResponse<PaymentDto>>(JsonOpts);
        Assert.True(pay!.Success);
        Assert.Equal(-50m, pay.Data!.Amount);
        Assert.Equal(31m, pay.Data.BalanceAfter);
        Assert.NotNull(pay.Data.CashSessionId);

        var account = await (await client.GetAsync($"/api/customers/{customerId}/account"))
            .Content.ReadFromJsonAsync<ApiResponse<AccountDto>>(JsonOpts);
        Assert.Equal(31m, account!.Data!.Balance);

        var sumAfterPay = await (await client.GetAsync("/api/cash/summary"))
            .Content.ReadFromJsonAsync<ApiResponse<SummaryDto>>(JsonOpts);
        Assert.Equal(90m, sumAfterPay!.Data!.TotalCashPayments);
        Assert.Equal(110m, sumAfterPay.Data.ProjectedAmount);
    }

    [Fact]
    public async Task AccountEndpoints_AreIsolated_ByTenant()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantA = $"t-cca-{Guid.NewGuid():N}";
        var tenantB = $"t-ccb-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantA);
        await SeedTenantAsync(factory, tenantB);

        using var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Test-TenantId", tenantA);
        clientA.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        await clientA.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        var productId = await CreateProductAsync(clientA, "SKU-ISO");
        var customerId = await CreateCustomerAsync(clientA, "Solo A", "20123456789");

        var saleRes = await clientA.PostAsJsonAsync(
            "/api/sales",
            new
            {
                customerId,
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 3, amount = 121m } }
            });
        Assert.Equal(HttpStatusCode.Created, saleRes.StatusCode);

        using var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Test-TenantId", tenantB);
        clientB.DefaultRequestHeaders.Add("X-Test-Roles", TenantRoleNames.Admin);

        var accountB = await clientB.GetAsync($"/api/customers/{customerId}/account");
        Assert.Equal(HttpStatusCode.NotFound, accountB.StatusCode);

        var movementsB = await clientB.GetAsync($"/api/customers/{customerId}/movements");
        Assert.Equal(HttpStatusCode.NotFound, movementsB.StatusCode);

        var payB = await clientB.PostAsJsonAsync(
            $"/api/customers/{customerId}/account/payments",
            new { amount = 10m, method = 1 });
        Assert.Equal(HttpStatusCode.NotFound, payB.StatusCode);
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

    private static async Task<Guid> CreateProductAsync(HttpClient client, string sku)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Prod CC",
                sku,
                barcode = "7791234567890",
                netPrice = 100m,
                taxRate = 21m,
                stock = 20,
                extendedDataJson = new { }
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var body = await createResponse.Content.ReadFromJsonAsync<ApiResponse<IdDto>>(JsonOpts);
        return body!.Data!.Id;
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client, string name, string taxId)
    {
        var res = await client.PostAsJsonAsync(
            "/api/customers",
            new { name, taxId, email = "", phone = "", address = "" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<IdDto>>(JsonOpts);
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
        public string? Message { get; set; }
    }

    private sealed class IdDto
    {
        public Guid Id { get; set; }
    }

    private sealed class SaleDto
    {
        public Guid Id { get; set; }
        public Guid? CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    private sealed class AccountDto
    {
        public Guid CustomerId { get; set; }
        public decimal Balance { get; set; }
        public List<MovementDto> RecentMovements { get; set; } = new();
    }

    private sealed class MovementDto
    {
        public int Type { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
    }

    private sealed class PaymentDto
    {
        public Guid MovementId { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public Guid? CashSessionId { get; set; }
    }

    private sealed class SummaryDto
    {
        public decimal TotalSales { get; set; }
        public decimal TotalCashPayments { get; set; }
        public decimal TotalCreditPayments { get; set; }
        public decimal ProjectedAmount { get; set; }
    }
}
