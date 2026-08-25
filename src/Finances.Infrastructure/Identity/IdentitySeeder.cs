using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Identity;

/// <summary>
/// Ensures the base roles exist and that there is exactly one super admin, driven entirely
/// by configuration (<c>AdminUser:Email</c> / <c>AdminUser:Password</c>).
///
/// The password is never hard-coded: it is read from configuration, which in practice means
/// an environment variable (<c>AdminUser__Password</c>) or user-secrets. If the configured
/// admin account does not exist yet and no password is provided, nothing is created and a
/// warning is logged (so we never fall back to a weak default).
///
/// Changing the super admin is done by changing <c>AdminUser:Email</c>: on the next startup
/// the configured account is promoted to Admin and every other admin is demoted to User,
/// keeping a single super admin. If the new email already belongs to a registered user, it is
/// simply promoted (no password needed); otherwise a password must be configured to create it.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("IdentitySeeder");

        foreach (var role in new[] { AuthService.AdminRole, AuthService.UserRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = config["AdminUser:Email"]?.Trim();
        var adminPassword = config["AdminUser:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            logger?.LogWarning("AdminUser:Email is not configured; skipping super-admin seeding.");
            return;
        }

        // Find (or create) the configured super admin account.
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                logger?.LogWarning(
                    "Super admin '{Email}' does not exist and no AdminUser:Password is configured. " +
                    "Set the AdminUser__Password environment variable (or a user-secret), or register " +
                    "that email as a normal user so it can be promoted automatically.",
                    adminEmail);
                return;
            }

            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Administrator",
                OnboardingCompleted = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await userManager.CreateAsync(admin, adminPassword);
            if (!created.Succeeded)
            {
                logger?.LogError(
                    "Could not create super admin '{Email}': {Errors}",
                    adminEmail, string.Join(" ", created.Errors.Select(e => e.Description)));
                return;
            }

            logger?.LogInformation("Super admin '{Email}' created.", adminEmail);
        }

        // Make sure the configured account holds the Admin role.
        if (!await userManager.IsInRoleAsync(admin, AuthService.AdminRole))
        {
            await userManager.AddToRoleAsync(admin, AuthService.AdminRole);
            logger?.LogInformation("Promoted '{Email}' to super admin.", adminEmail);
        }

        // Enforce a single super admin: demote any other admin to a normal user. This is what
        // makes "changing the admin" work — point AdminUser:Email at a new account and restart.
        var currentAdmins = await userManager.GetUsersInRoleAsync(AuthService.AdminRole);
        foreach (var other in currentAdmins)
        {
            if (string.Equals(other.Email, adminEmail, StringComparison.OrdinalIgnoreCase)) continue;

            await userManager.RemoveFromRoleAsync(other, AuthService.AdminRole);
            if (!await userManager.IsInRoleAsync(other, AuthService.UserRole))
                await userManager.AddToRoleAsync(other, AuthService.UserRole);

            logger?.LogInformation("Demoted previous admin '{Email}' to user.", other.Email);
        }
    }
}
