using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Contracts.Platform;
using POS.Domain.Entities;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformTenantUsersIntegrationTests
{
    [Fact]
    public async Task List_Block_Login403_Unblock_LoginOk()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var email = $"p6-{Guid.NewGuid():N}@test.local";
        var createTenantRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Tenant usuarios",
                ContactEmail = null,
                AdminEmail = email,
                AdminFullName = "Usuario P6",
                AdminPassword = "Pass123!"
            });
        Assert.Equal(HttpStatusCode.OK, createTenantRes.StatusCode);
        var created = await createTenantRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var appUser = await users.FindByEmailAsync(email);
            Assert.NotNull(appUser);
            userId = appUser!.Id;
        }

        var listRes = await platformClient.GetAsync($"/api/platform/tenants/{tenantId}/users?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var listBody = await listRes.Content.ReadFromJsonAsync<ApiResponse<TenantUserDirectoryPageDto>>();
        Assert.Contains(listBody!.Data!.Items, u => u.Id == userId && !u.BlockedByPlatform);

        var blockRes = await platformClient.PostAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/users/{userId}/block",
            new PlatformUserActionRequest { Justification = "Bloqueo de prueba integración" });
        Assert.Equal(HttpStatusCode.OK, blockRes.StatusCode);

        using var loginClient = factory.CreateClient();
        var loginBlocked = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Pass123!" });
        Assert.Equal(HttpStatusCode.Forbidden, loginBlocked.StatusCode);

        var unblockRes = await platformClient.PostAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/users/{userId}/unblock",
            new PlatformUserActionRequest { Justification = "Desbloqueo de prueba integración" });
        Assert.Equal(HttpStatusCode.OK, unblockRes.StatusCode);

        var loginOk = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Pass123!" });
        Assert.Equal(HttpStatusCode.OK, loginOk.StatusCode);
    }

    [Fact]
    public async Task RequestPasswordReset_ReturnsAck()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        factory.EmailSender.Clear();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var em = $"r-{Guid.NewGuid():N}@test.local";
        var createTenantRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Tenant reset",
                ContactEmail = null,
                AdminEmail = em,
                AdminFullName = "R",
                AdminPassword = "Pass123!"
            });
        Assert.Equal(HttpStatusCode.OK, createTenantRes.StatusCode);
        var created = await createTenantRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var appUser = await users.FindByEmailAsync(em);
            Assert.NotNull(appUser);
            userId = appUser!.Id;
        }

        var res = await platformClient.PostAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/users/{userId}/request-password-reset",
            new PlatformUserActionRequest { Justification = "Solicitud de reset por soporte" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<PlatformMutationAckDto>>();
        Assert.True(body?.Success);
        Assert.NotNull(body?.Data?.Message);
        Assert.Contains("envió", body!.Data!.Message, StringComparison.OrdinalIgnoreCase);

        var sent = factory.EmailSender.Sent;
        Assert.Single(sent);
        Assert.Equal(em, sent[0].To);
        Assert.Contains("Restablecer", sent[0].Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Token:", sent[0].PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResendEmailConfirmation_SendsMail_WhenUnconfirmed()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        factory.EmailSender.Clear();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var em = $"c-{Guid.NewGuid():N}@test.local";
        var createTenantRes = await platformClient.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantApiRequest
            {
                Name = "Tenant confirm",
                ContactEmail = null,
                AdminEmail = em,
                AdminFullName = "C",
                AdminPassword = "Pass123!"
            });
        Assert.Equal(HttpStatusCode.OK, createTenantRes.StatusCode);
        var created = await createTenantRes.Content.ReadFromJsonAsync<ApiResponse<TenantDetailDto>>();
        var tenantId = created!.Data!.Id;

        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var appUser = await users.FindByEmailAsync(em);
            Assert.NotNull(appUser);
            userId = appUser!.Id;
            appUser.EmailConfirmed = false;
            var update = await users.UpdateAsync(appUser);
            Assert.True(update.Succeeded);
        }

        var res = await platformClient.PostAsJsonAsync(
            $"/api/platform/tenants/{tenantId}/users/{userId}/resend-email-confirmation",
            new PlatformUserActionRequest { Justification = "Reenvío de confirmación por soporte" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<PlatformMutationAckDto>>();
        Assert.True(body?.Success);

        var sent = factory.EmailSender.Sent;
        Assert.Single(sent);
        Assert.Equal(em, sent[0].To);
        Assert.Contains("Confirmá", sent[0].Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListUsers_UnknownTenant_Returns404()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var platformClient = factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var res = await platformClient.GetAsync("/api/platform/tenants/__no_existe__/users");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private sealed class ApiResponse<T>
    {
        public bool Success { get; init; }

        public T? Data { get; init; }
    }
}
