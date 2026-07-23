using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Interfaces;
using POS.Application.Platform;
using POS.Domain.Platform;
using POS.Domain.Tenant;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Platform;
using POS.Infrastructure.Services;
using POS.Infrastructure.TenantUsers;
using Xunit;

namespace POS.API.IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestAuthScheme = "TestAuth";
    private readonly string _testDbName = $"PosFacturacion_IntegrationTests_{Guid.NewGuid():N}";
    private readonly RecordingEmailSender _emailSender = new();
    private readonly string _billingProvider;

    public TestWebApplicationFactory(string billingProvider = "Manual") =>
        _billingProvider = billingProvider;

    public RecordingEmailSender EmailSender => _emailSender;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddInMemoryCollection(
                [
                    new KeyValuePair<string, string?>("AdminSeed:Enabled", "false"),
                    new KeyValuePair<string, string?>("PlatformAdminSeed:Enabled", "false"),
                    new KeyValuePair<string, string?>("Jwt:Issuer", "POS"),
                    new KeyValuePair<string, string?>("Jwt:Audience", "pos-clients"),
                    new KeyValuePair<string, string?>("Jwt:SigningKey", "TESTING-SIGNING-KEY-MINIMUM-32-CHARS!!"),
                    new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", BuildConnectionString()),
                    new KeyValuePair<string, string?>("Email:Provider", "Logging"),
                    new KeyValuePair<string, string?>("Email:FromAddress", "noreply@test.local"),
                    new KeyValuePair<string, string?>("Email:PublicAppBaseUrl", "http://localhost:4200"),
                    new KeyValuePair<string, string?>("Billing:Provider", _billingProvider),
                    new KeyValuePair<string, string?>("Billing:EnableRenewalJob", "false"),
                    new KeyValuePair<string, string?>("Billing:EnableDunningJob", "false")
                ]);
            });

        builder.ConfigureServices(
            services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));
                services.RemoveAll(typeof(IEmailSender));

                services.AddSingleton<IEmailSender>(_emailSender);
                services.AddSingleton(_emailSender);

                services.AddDbContext<ApplicationDbContext>(
                    options => options.UseSqlServer(BuildConnectionString()));

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthScheme;
                        options.DefaultChallengeScheme = TestAuthScheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthScheme, _ => { });
            });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TestInit");
        await PlatformRoleSeeder.EnsurePlatformRolesAsync(roleManager, logger);
        await TenantRoleSeeder.EnsureTenantRolesAsync(roleManager, logger);
    }

    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
    }

    private string BuildConnectionString() =>
        $"Server=(localdb)\\mssqllocaldb;Database={_testDbName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (string.Equals(
                    Request.Headers["X-Test-Auth"].FirstOrDefault(),
                    "anonymous",
                    StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.NoResult());

            var userId = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "user-a";
            var claims = new List<Claim>
            {
                new Claim("sub", userId),
                new Claim(ClaimTypes.NameIdentifier, userId)
            };

            if (string.Equals(
                    Request.Headers["X-Test-Impersonation"].FirstOrDefault(),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                var tenantId = Request.Headers["X-Test-TenantId"].FirstOrDefault() ?? "tenant-a";
                claims.Add(new Claim(CurrentUserService.TenantIdClaimType, tenantId));
                claims.Add(new Claim(ClaimTypes.Role, TenantRoleNames.Admin));
                claims.Add(new Claim(PlatformClaimTypes.Impersonation, "true"));
                var impReason = Request.Headers["X-Test-ImpersonationReason"].FirstOrDefault() ?? "test";
                claims.Add(new Claim(PlatformClaimTypes.ImpersonationReason, impReason));
            }
            else if (string.Equals(
                    Request.Headers["X-Test-Platform"].FirstOrDefault(),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(PlatformClaimTypes.IsPlatform, "true"));
                var role = Request.Headers["X-Test-PlatformRole"].FirstOrDefault()
                    ?? PlatformRoleNames.Operations;
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            else
            {
                var tenantId = Request.Headers["X-Test-TenantId"].FirstOrDefault() ?? "tenant-a";
                claims.Add(new Claim(CurrentUserService.TenantIdClaimType, tenantId));
                var rolesHeader = Request.Headers["X-Test-Roles"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(rolesHeader))
                {
                    claims.Add(new Claim(ClaimTypes.Role, TenantRoleNames.Admin));
                }
                else
                {
                    foreach (var role in rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            var identity = new ClaimsIdentity(claims, TestAuthScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, TestAuthScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
