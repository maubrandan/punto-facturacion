using Microsoft.AspNetCore.Identity;
using POS.Application.Contracts.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;

namespace POS.Infrastructure.Platform;

internal static class PlatformOperatorMappings
{
    public static async Task<PlatformOperatorSummaryDto> ToSummaryAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var platformRole = roles.FirstOrDefault(PlatformRoleNames.IsKnownRole) ?? string.Empty;

        return new PlatformOperatorSummaryDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            PlatformRole = platformRole,
            EmailConfirmed = user.EmailConfirmed,
            BlockedByPlatform = user.BlockedByPlatform
        };
    }
}
