using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using CoCoReTrade.Application;

namespace CoCoReTrade.Infrastructure;

public static class IdentitySeeder
{
    public static async Task SeedAsync(AppDbContext db, RoleManager<IdentityRole> roles,
        UserManager<ApplicationUser> users, IConfiguration configuration)
    {
        await db.Database.MigrateAsync();
        foreach (var name in new[] { "User", "Inspector", "Admin" })
            if (!await roles.RoleExistsAsync(name)) await roles.CreateAsync(new IdentityRole(name));

        foreach (var (roleName, permission) in new[]
        {
            ("Inspector", StaffPermissions.ReadPending), ("Inspector", StaffPermissions.Inspect),
            ("Admin", StaffPermissions.ReadPending), ("Admin", StaffPermissions.Inspect),
            ("Admin", StaffPermissions.Publish), ("Admin", StaffPermissions.Reject),
            ("Admin", StaffPermissions.ManageProducts), ("Admin", StaffPermissions.ManageOrders),
            ("Admin", StaffPermissions.ManageUsers), ("Admin", StaffPermissions.ManageWallets),
            ("Admin", StaffPermissions.ReadAudit)
        })
        {
            var role = (await roles.FindByNameAsync(roleName))!;
            if (!(await roles.GetClaimsAsync(role)).Any(x => x.Type == StaffPermissions.ClaimType && x.Value == permission))
                await roles.AddClaimAsync(role, new Claim(StaffPermissions.ClaimType, permission));
        }

        const string email = "admin@cocoretrade.vn";
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            var password = Environment.GetEnvironmentVariable("COCO_ADMIN_INITIAL_PASSWORD") ?? configuration["Admin:InitialPassword"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Set COCO_ADMIN_INITIAL_PASSWORD or the Admin:InitialPassword development secret before first run.");
            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true,
                PhoneNumber = "0905123456", PhoneNumberConfirmed = true, FullName = "CoCo Admin", MustChangePassword = true };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded) throw new InvalidOperationException("Cannot seed admin: " + string.Join("; ", result.Errors.Select(x => x.Description)));
        }
        await EnsureAdminRolesAsync(users, admin);

        const string additionalEmail = "admin1@cocoretrade.vn";
        var additionalPassword = Environment.GetEnvironmentVariable("COCO_ADMIN1_INITIAL_PASSWORD");
        var additionalAdmin = await users.FindByEmailAsync(additionalEmail);
        if (additionalAdmin is null && !string.IsNullOrWhiteSpace(additionalPassword))
        {
            additionalAdmin = new ApplicationUser
            {
                UserName = additionalEmail,
                Email = additionalEmail,
                EmailConfirmed = true,
                FullName = "admin1",
                MustChangePassword = true
            };
            var result = await users.CreateAsync(additionalAdmin, additionalPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException("Cannot seed admin1: " + string.Join("; ", result.Errors.Select(x => x.Description)));
            await EnsureAdminRolesAsync(users, additionalAdmin);
        }
        else if (additionalAdmin is not null)
        {
            if (!await users.IsInRoleAsync(additionalAdmin, "Admin"))
            {
                if (!string.IsNullOrWhiteSpace(additionalPassword))
                    throw new InvalidOperationException("admin1 email already belongs to a non-admin account; refusing automatic promotion.");
            }
            else await EnsureAdminRolesAsync(users, additionalAdmin);
        }

        if (await users.FindByIdAsync("system") is null)
        {
            var system = new ApplicationUser
            {
                Id = "system", UserName = "system@cocoretrade.vn", Email = "system@cocoretrade.vn",
                EmailConfirmed = true, FullName = "CoCo System", LockoutEnabled = true,
                LockoutEnd = DateTimeOffset.MaxValue
            };
            var result = await users.CreateAsync(system);
            if (!result.Succeeded)
                throw new InvalidOperationException("Cannot seed system actor: " + string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }

    private static async Task EnsureAdminRolesAsync(UserManager<ApplicationUser> users, ApplicationUser admin)
    {
        foreach (var role in new[] { "User", "Inspector", "Admin" })
        {
            if (await users.IsInRoleAsync(admin, role)) continue;
            var result = await users.AddToRoleAsync(admin, role);
            if (!result.Succeeded)
                throw new InvalidOperationException("Cannot assign admin role: " + string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }
}
