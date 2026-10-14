using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Identity;

namespace CoCoReTrade.Infrastructure;

public sealed class StaffPermissionChecker(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    : IStaffPermissionChecker
{
    public async Task EnsureAsync(string actorId, string permission, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) throw new UnauthorizedAccessException("Sign in is required.");
        var user = await users.FindByIdAsync(actorId);
        if (user is null || user.TrustSuspended || user.LockoutEnd > DateTimeOffset.UtcNow)
            throw new UnauthorizedAccessException("Account is unavailable.");
        var userRoles = await users.GetRolesAsync(user);
        if (!userRoles.Any(x => StaffPermissions.RequiredRoles(permission).Contains(x)))
            throw new UnauthorizedAccessException("This account does not have the required staff role.");
        if ((await users.GetClaimsAsync(user)).Any(x => x.Type == StaffPermissions.ClaimType && x.Value == permission)) return;
        foreach (var roleName in userRoles)
        {
            var role = await roles.FindByNameAsync(roleName);
            if (role is not null && (await roles.GetClaimsAsync(role)).Any(x => x.Type == StaffPermissions.ClaimType && x.Value == permission))
                return;
        }
        throw new UnauthorizedAccessException("This account does not have the required permission.");
    }
}
