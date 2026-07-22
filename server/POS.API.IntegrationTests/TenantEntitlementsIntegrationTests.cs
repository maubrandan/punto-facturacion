using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Contracts;
using POS.Application.Contracts.Platform;
using POS.Application.Contracts.Products;
using POS.Application.Contracts.Sales;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Platform;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class TenantEntitlementsIntegrationTests
{
    [Fact]
    public async Task MaxProducts_Stops_Second_Product()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = await CreateTenantViaPlatformAsync(factory);

        using (var pc = factory.CreateClient())
        {
            pc.DefaultRequestHeaders.Add("X-Test-Platform", "true");
            var putRes = await pc.PutAsJsonAsync(
                $"/api/platform/tenants/{tenantId}/entitlements",
                new
                {
                    maxProducts = 1,
                    maxTenantUsers = (int?)null,
                    salesEnabled = true,
                    justification = "Límite 1 prod en tests"
                });
            Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        }

        using var tenantClient = factory.CreateClient();
        tenantClient.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        await PostProductEnsureCreatedAsync(tenantClient, "SKU-Q1");

        var p2 = await tenantClient.PostAsJsonAsync(
            "/api/products",
            BuildProductPayload("SKU-Q2"));

        Assert.Equal(HttpStatusCode.BadRequest, p2.StatusCode);
        var err = await p2.Content.ReadFromJsonAsync<ApiResponse<ProductResponse>>();
        Assert.NotNull(err?.Error);
        Assert.Equal("entitlement.product_limit_reached", err!.Error!.Code);
    }

    [Fact]
    public async Task SalesDisabled_Blocks_Sale()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = await CreateTenantViaPlatformAsync(factory);

        using (var pc = factory.CreateClient())
        {
            pc.DefaultRequestHeaders.Add("X-Test-Platform", "true");
            var putRes = await pc.PutAsJsonAsync(
                $"/api/platform/tenants/{tenantId}/entitlements",
                new
                {
                    maxProducts = (int?)null,
                    maxTenantUsers = (int?)null,
                    salesEnabled = false,
                    justification = "Cortar ventas en tests"
                });
            Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId);

        var open = await client.PostAsJsonAsync("/api/cash/open", new { initialAmount = 10m });
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        var productId = await PostProductEnsureCreatedAsync(client, "SKU-SOFF");

        var saleRes = await client.PostAsJsonAsync(
            "/api/sales",
            new
            {
                lines = new[] { new { productId, quantity = 1 } },
                payments = new[] { new { method = 0, amount = 121m } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, saleRes.StatusCode);
        var saleErr = await saleRes.Content.ReadFromJsonAsync<ApiResponse<SaleResponse>>();
        Assert.Equal("entitlement.sales_disabled", saleErr?.Error?.Code);
    }

    [Fact]
    public async Task UserLimit_Guard_Fails_When_Max_Reached()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantId = await CreateTenantViaPlatformAsync(factory);

        using (var pc = factory.CreateClient())
        {
            pc.DefaultRequestHeaders.Add("X-Test-Platform", "true");
            var putRes = await pc.PutAsJsonAsync(
                $"/api/platform/tenants/{tenantId}/entitlements",
                new
                {
                    maxProducts = (int?)null,
                    maxTenantUsers = 1,
                    salesEnabled = true,
                    justification = "Máximo 1 usuario en tests"
                });
            Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<ITenantEntitlementGuard>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var before = await guard.EnsureCanAddTenantUserAsync(tenantId);
        Assert.True(before.IsSuccess);

        var email = $"u1-{Guid.NewGuid():N}@t.local";
        var u = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            TenantId = tenantId,
            FullName = "U1",
            BusinessType = "Kiosco",
            AccountKind = UserAccountKind.TenantUser
        };
        Assert.True((await users.CreateAsync(u, "Pass123!")).Succeeded);

        var after = await guard.EnsureCanAddTenantUserAsync(tenantId);
        Assert.False(after.IsSuccess);
        Assert.Equal("entitlement.user_limit_reached", after.ErrorCode);
    }

    [Fact]
    public async Task GetEntitlements_Defaults_When_No_Row()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var tenantId = await CreateTenantViaPlatformAsync(factory);

        using var pc = factory.CreateClient();
        pc.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var res = await pc.GetAsync($"/api/platform/tenants/{tenantId}/entitlements");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<TenantEntitlementsDto>>();
        Assert.True(body?.Success);
        Assert.Null(body?.Data?.MaxProducts);
        Assert.Null(body?.Data?.MaxTenantUsers);
        Assert.True(body?.Data?.SalesEnabled);
    }

    private static async Task<string> CreateTenantViaPlatformAsync(TestWebApplicationFactory factory)
    {
        using var pc = factory.CreateClient();
        pc.DefaultRequestHeaders.Add("X-Test-Platform", "true");
        var createRes = await pc.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest { Name = $"Ent F9 {Guid.NewGuid():N}", ContactEmail = null });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        return created!.Data!.Id;
    }

    private static object BuildProductPayload(string sku) => new
    {
        name = "P",
        sku,
        barcode = "",
        netPrice = 10m,
        taxRate = 0m,
        stock = 1,
        extendedDataJson = new { }
    };

    private static async Task<Guid> PostProductEnsureCreatedAsync(HttpClient client, string sku)
    {
        var createResponse = await client.PostAsJsonAsync("/api/products", BuildProductPayload(sku));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var body = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProductResponse>>();
        Assert.NotNull(body?.Data?.Id);
        return body!.Data!.Id;
    }

}
