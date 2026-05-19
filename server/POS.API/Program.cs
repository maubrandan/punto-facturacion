using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using POS.API;
using POS.API.Middleware;
using POS.Domain.Entities;
using POS.Infrastructure;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// Identity (stateless: sin cookies; los tokens se emiten/validan con JWT)
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Relajamos las reglas para desarrollo
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 4; // Con 6 para '123456' sobra
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"]
    ?? throw new InvalidOperationException("Falta la sección o la clave Jwt:SigningKey en la configuración.");
var signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
if (signingKeyBytes.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 bytes (256 bits) para HS256.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"] ?? "POS",
            ValidateAudience = true,
            ValidAudience = jwt["Audience"] ?? "pos-clients",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
        };
    });

builder.Services.AddPlatformAuthorizationPolicies();

builder.Services.AddControllers();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapGet(
        "/debug/admin-seed-status",
        async (UserManager<ApplicationUser> userManager, IConfiguration configuration) =>
        {
            var seedOptions = configuration.GetSection(AdminSeedOptions.SectionName).Get<AdminSeedOptions>()
                ?? new AdminSeedOptions();
            var adminExists = await userManager.FindByEmailAsync(seedOptions.Email.Trim()) is not null;

            return Results.Ok(new
            {
                seedEnabled = seedOptions.Enabled,
                seedEmail = seedOptions.Email,
                adminExists
            });
        });
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Text("OK", "text/plain"))
    .WithName("Health")
    .ExcludeFromDescription();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

await DbInitializer.InitializeAsync(app.Services);

app.Run();

public partial class Program;

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
