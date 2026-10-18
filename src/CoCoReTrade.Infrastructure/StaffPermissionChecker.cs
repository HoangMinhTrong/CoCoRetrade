using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Identity;

namespace CoCoReTrade.Infrastructure;

public sealed class StaffPermissionChecker(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    : IStaffPermissionChecker
{
    public async Task EnsureAsync(string actorId, string permission, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) throw new UnauthorizedAccessException("Hãy đăng nhập.");
        var user = await users.FindByIdAsync(actorId);
        if (user is null || user.TrustSuspended || user.LockoutEnd > DateTimeOffset.UtcNow)
            throw new UnauthorizedAccessException("Tài khoản không khả dụng.");
        var userRoles = await users.GetRolesAsync(user);
        if (!userRoles.Any(x => StaffPermissions.RequiredRoles(permission).Contains(x)))
            throw new UnauthorizedAccessException("Tài khoản này không có vai trò nhân viên cần thiết.");
        if ((await users.GetClaimsAsync(user)).Any(x => x.Type == StaffPermissions.ClaimType && x.Value == permission)) return;
        foreach (var roleName in userRoles)
        {
            var role = await roles.FindByNameAsync(roleName);
            if (role is not null && (await roles.GetClaimsAsync(role)).Any(x => x.Type == StaffPermissions.ClaimType && x.Value == permission))
                return;
        }
        throw new UnauthorizedAccessException("Tài khoản này không có quyền thực hiện thao tác.");
    }
}
