using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Contracts;
using POS.Application.Contracts.Auth;
using POS.Application.Contracts.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Platform;
using POS.Infrastructure.Services;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class ImpersonationSessionJwtIntegrationTests
{
    private static readonly JsonSerializerOptions s_jsonCamel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task ImpersonationToken_CannotAccessPlatformRoutes_HasExpectedClaims()
    {
        await using var factory = new JwtBearerIntegrationTestFactory();
        await factory.InitializeAsync();

        string tenantId;
        string email;
        const string password = "Pass123!";

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Test");
            await PlatformRoleSeeder.EnsurePlatformRolesAsync(roleManager, log);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            tenantId = Guid.NewGuid().ToString("N");
            db.Tenants.Add(
                new Tenant
                {
                    Id = tenantId,
                    Name = "Suplantación JWT",
                    Status = TenantStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync();

            email = $"imp-jwt-{Guid.NewGuid():N}@test.local";
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var platformUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = PlatformScope.ReservedTenantId,
                FullName = "Operador JWT",
                BusinessType = PlatformScope.PlaceholderBusinessType,
                AccountKind = UserAccountKind.PlatformUser
            };
            var created = await users.CreateAsync(platformUser, password);
            Assert.True(created.Succeeded);
            var roleResult = await users.AddToRoleAsync(platformUser, PlatformRoleNames.Support);
            Assert.True(roleResult.Succeeded);
        }

        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var loginRes = await client.PostAsJsonAsync(
            "/api/platform/auth/login",
            new { email, password },
            s_jsonCamel);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginBody = await loginRes.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(s_jsonCamel);
        Assert.NotNull(loginBody?.Data?.AccessToken);
        var platformToken = loginBody!.Data!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);
        var impRes = await client.PostAsJsonAsync(
            "/api/platform/support/impersonation/session",
            new { tenantId, reason = "Prueba integración JWT", ttlMinutes = 15 },
            s_jsonCamel);
        Assert.Equal(HttpStatusCode.OK, impRes.StatusCode);
        var impBody = await impRes.Content.ReadFromJsonAsync<ApiResponse<ImpersonationSessionResponseDto>>(s_jsonCamel);
        Assert.NotNull(impBody?.Data?.AccessToken);
        var impersonationToken = impBody!.Data!.AccessToken;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(impersonationToken);
        Assert.Contains(jwt.Claims, c => c.Type == PlatformClaimTypes.Impersonation && c.Value == "true");
        Assert.DoesNotContain(jwt.Claims, c => c.Type == PlatformClaimTypes.IsPlatform);
        Assert.Contains(jwt.Claims, c => c.Type == CurrentUserService.TenantIdClaimType && c.Value == tenantId);

        using var tenantsClient = factory.CreateClient(new() { AllowAutoRedirect = false });
        tenantsClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", impersonationToken);
        var forbidden = await tenantsClient.GetAsync("/api/platform/tenants?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
