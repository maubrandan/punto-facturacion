using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using POS.Application.Interfaces;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opt;

    public JwtTokenService(IOptions<JwtOptions> options) => _opt = options.Value;

    public string CreateToken(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(user.TenantId))
            throw new InvalidOperationException("El usuario no tiene TenantId; no se puede emitir un JWT multi-tenant.");

        var keyBytes = Encoding.UTF8.GetBytes(_opt.SigningKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 bytes (256 bits) para HS256.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(CurrentUserService.TenantIdClaimType, user.TenantId),
            new("business_type", user.BusinessType),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_opt.ExpiresInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreatePlatformToken(
        ApplicationUser user,
        IReadOnlyList<string> platformRoles,
        CancellationToken cancellationToken = default)
    {
        if (platformRoles is null || platformRoles.Count == 0)
            throw new ArgumentException("Se requiere al menos un rol de plataforma.", nameof(platformRoles));

        var roles = platformRoles
            .Where(r => PlatformRoleNames.IsKnownRole(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (roles.Count == 0)
            throw new InvalidOperationException("Ningún rol es un rol Platform.* conocido.");

        var keyBytes = Encoding.UTF8.GetBytes(_opt.SigningKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 bytes (256 bits) para HS256.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(PlatformClaimTypes.IsPlatform, "true"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        claims.Add(new Claim(PlatformClaimTypes.PlatformRole, roles[0]));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_opt.ExpiresInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateImpersonationToken(
        ApplicationUser platformOperator,
        string targetTenantId,
        string reason,
        int ttlMinutes,
        CancellationToken cancellationToken = default)
    {
        if (platformOperator.AccountKind != UserAccountKind.PlatformUser)
            throw new InvalidOperationException("Solo cuentas de plataforma pueden emitir token de suplantación.");

        if (string.IsNullOrWhiteSpace(targetTenantId))
            throw new ArgumentException("Tenant objetivo obligatorio.", nameof(targetTenantId));

        var keyBytes = Encoding.UTF8.GetBytes(_opt.SigningKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 bytes (256 bits) para HS256.");

        var ttl = Math.Clamp(ttlMinutes, 1, 60);
        var reasonTrimmed = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, platformOperator.Id),
            new(ClaimTypes.NameIdentifier, platformOperator.Id),
            new(ClaimTypes.Email, platformOperator.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, platformOperator.Email ?? string.Empty),
            new(CurrentUserService.TenantIdClaimType, targetTenantId.Trim()),
            new("business_type", platformOperator.BusinessType),
            new(PlatformClaimTypes.Impersonation, "true"),
            new(PlatformClaimTypes.ImpersonationReason, TruncateReason(reasonTrimmed, 200)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(ttl),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string TruncateReason(string reason, int max)
    {
        if (reason.Length <= max)
            return reason;
        return reason[..max];
    }
}
