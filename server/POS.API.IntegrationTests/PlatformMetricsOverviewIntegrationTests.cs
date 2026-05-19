using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Contracts.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class PlatformMetricsOverviewIntegrationTests
{
    private static readonly JsonSerializerOptions s_jsonInsensitive = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Overview_WithSeededData_ReturnsExpectedAggregateCounts()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var tenantActive = $"t-metrics-a-{Guid.NewGuid():N}";
        var tenantSuspended = $"t-metrics-s-{Guid.NewGuid():N}";
        var tenantClosed = $"t-metrics-c-{Guid.NewGuid():N}";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Tenants.AddRange(
                new Tenant { Id = tenantActive, Name = "MA", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow },
                new Tenant { Id = tenantSuspended, Name = "MS", Status = TenantStatus.Suspended, CreatedAt = DateTime.UtcNow },
                new Tenant { Id = tenantClosed, Name = "MC", Status = TenantStatus.Closed, CreatedAt = DateTime.UtcNow });

            var email = $"metrics-{Guid.NewGuid():N}@test.local";
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var appUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = tenantActive,
                FullName = "Metrics blocked",
                BusinessType = "Kiosco",
                AccountKind = UserAccountKind.TenantUser,
                BlockedByPlatform = true,
            };

            Assert.True((await users.CreateAsync(appUser, "Pass123!")).Succeeded);

            db.PlatformAuditEvents.AddRange(
                new PlatformAuditEvent
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    Action = "Test.MetricsRecent",
                    IsImpersonationContext = false,
                },
                new PlatformAuditEvent
                {
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-25),
                    Action = "Test.MetricsStale",
                    IsImpersonationContext = false,
                });

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Platform", "true");

        var response = await client.GetAsync("/api/platform/metrics/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WrappedApiResponse<PlatformMetricsOverviewDto>>(s_jsonInsensitive);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var d = body.Data!;
        Assert.Equal(3, d.TotalTenants);
        Assert.Equal(1, d.ActiveTenants);
        Assert.Equal(1, d.SuspendedTenants);
        Assert.Equal(1, d.ClosedTenants);
        Assert.Equal(1, d.BlockedTenantUsers);
        Assert.Equal(1, d.RecentAuditEvents);
    }

    private sealed class WrappedApiResponse<T>
    {
        public bool Success { get; init; }

        public T? Data { get; init; }
    }
}
