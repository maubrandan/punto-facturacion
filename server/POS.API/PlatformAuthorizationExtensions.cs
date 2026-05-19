using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Platform;
using POS.Domain.Platform;

namespace POS.API;

internal static class PlatformAuthorizationExtensions
{
    public static IServiceCollection AddPlatformAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.PlatformUser,
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(PlatformClaimTypes.IsPlatform, "true");
                    policy.RequireAssertion(ctx =>
                        PlatformRoleNames.All.Any(role => ctx.User.IsInRole(role)));
                });

            options.AddPolicy(
                AuthorizationPolicies.PlatformReadOnly,
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(PlatformClaimTypes.IsPlatform, "true");
                    policy.RequireAssertion(ctx =>
                        ctx.User.IsInRole(PlatformRoleNames.SupportReadOnly)
                        || ctx.User.IsInRole(PlatformRoleNames.Support));
                });

            options.AddPolicy(
                AuthorizationPolicies.PlatformOperations,
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(PlatformClaimTypes.IsPlatform, "true");
                    policy.RequireAssertion(ctx =>
                        ctx.User.IsInRole(PlatformRoleNames.Operations)
                        || ctx.User.IsInRole(PlatformRoleNames.SuperAdmin));
                });

            options.AddPolicy(
                AuthorizationPolicies.PlatformSuperAdmin,
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(PlatformClaimTypes.IsPlatform, "true");
                    policy.RequireRole(PlatformRoleNames.SuperAdmin);
                });

            options.AddPolicy(
                AuthorizationPolicies.PlatformImpersonation,
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(PlatformClaimTypes.IsPlatform, "true");
                    policy.RequireAssertion(ctx =>
                        ctx.User.IsInRole(PlatformRoleNames.Support)
                        || ctx.User.IsInRole(PlatformRoleNames.SupportReadOnly)
                        || ctx.User.IsInRole(PlatformRoleNames.Operations)
                        || ctx.User.IsInRole(PlatformRoleNames.SuperAdmin));
                });
        });

        return services;
    }
}
