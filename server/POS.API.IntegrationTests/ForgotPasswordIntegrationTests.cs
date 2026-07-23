using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Domain.Tenant;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class ForgotPasswordIntegrationTests
{
    [Fact]
    public async Task ForgotPassword_TenantUser_SendsEmailAndReturnsGenericAck()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        factory.EmailSender.Clear();

        var tenantId = $"t-forgot-{Guid.NewGuid():N}";
        await SeedTenantAsync(factory, tenantId);

        var email = $"forgot-{Guid.NewGuid():N}@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = tenantId,
                FullName = "Forgot Target",
                BusinessType = BusinessTypeNames.Kiosco,
                AccountKind = UserAccountKind.TenantUser
            };
            Assert.True((await users.CreateAsync(user, "Pass123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, TenantRoleNames.Cashier)).Succeeded);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");

        var res = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<AuthMessageDto>>();
        Assert.True(body?.Success);
        Assert.Contains("Si el email está registrado", body!.Data!.Message, StringComparison.Ordinal);

        var sent = factory.EmailSender.Sent;
        Assert.Single(sent);
        Assert.Equal(email, sent[0].To);
        Assert.Contains("/reset-password?", sent[0].PlainTextBody, StringComparison.Ordinal);
        Assert.Contains("Token:", sent[0].PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsSameAckWithoutEmail()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        factory.EmailSender.Clear();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");

        var res = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = $"missing-{Guid.NewGuid():N}@test.local" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<AuthMessageDto>>();
        Assert.True(body?.Success);
        Assert.Contains("Si el email está registrado", body!.Data!.Message, StringComparison.Ordinal);
        Assert.Empty(factory.EmailSender.Sent);
    }

    [Fact]
    public async Task ForgotPassword_PlatformUser_DoesNotSendEmail()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        factory.EmailSender.Clear();

        var email = $"plat-forgot-{Guid.NewGuid():N}@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = PlatformScope.ReservedTenantId,
                FullName = "Platform Op",
                BusinessType = PlatformScope.PlaceholderBusinessType,
                AccountKind = UserAccountKind.PlatformUser
            };
            Assert.True((await users.CreateAsync(user, "Pass123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, PlatformRoleNames.Operations)).Succeeded);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");

        var res = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<AuthMessageDto>>();
        Assert.True(body?.Success);
        Assert.Empty(factory.EmailSender.Sent);
    }

    [Fact]
    public async Task ForgotPassword_EmptyEmail_ReturnsBadRequest()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");

        var res = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<AuthMessageDto>>();
        Assert.False(body?.Success);
        Assert.Equal("auth.forgot.validation", body?.Error?.Code);
    }

    private static async Task SeedTenantAsync(TestWebApplicationFactory factory, string tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Tenants.Add(
            new Tenant
            {
                Id = tenantId,
                Name = "Forgot Password Test",
                BusinessType = BusinessTypeNames.Kiosco,
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
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

    private sealed class AuthMessageDto
    {
        public string Message { get; set; } = string.Empty;
    }
}
