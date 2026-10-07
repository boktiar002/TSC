using Microsoft.AspNetCore.Identity;
using TSC.Models;

namespace TSC.Data;

public static class DbSeeder
{
    public const string AdminRole = "Admin";

    private static readonly string[] Roles = { AdminRole, "Teacher", "Student" };

    // Structural: the app cannot authorise anything without these, in any environment.
    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Development convenience only. These credentials are in the README and in git history,
    // so seeding them on a server would be handing out a key. Program.cs gates this on
    // IsDevelopment; on a server use `dotnet run -- create-admin <email> <password>`.
    public static async Task SeedDevelopmentAdminAsync(IServiceProvider serviceProvider)
    {
        const string adminEmail = "admin@tsc.local";
        const string adminPassword = "Admin@12345";

        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByEmailAsync(adminEmail) != null)
            return;

        await CreateAdminAsync(userManager, adminEmail, adminPassword, "System Administrator");
    }

    public static async Task<string?> CreateAdminAsync(
        UserManager<ApplicationUser> userManager, string email, string password, string fullName)
    {
        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName
        };

        var result = await userManager.CreateAsync(admin, password);

        if (!result.Succeeded)
            return string.Join(" ", result.Errors.Select(e => e.Description));

        var inRole = await userManager.AddToRoleAsync(admin, AdminRole);

        if (!inRole.Succeeded)
        {
            // Don't leave an admin-less orphan that can log in but see nothing.
            await userManager.DeleteAsync(admin);
            return string.Join(" ", inRole.Errors.Select(e => e.Description));
        }

        return null;
    }

    // A server's first admin. The dev account above is a published credential and the
    // `create-admin` command needs a shell, which a container host may not give you -- so the
    // one path that always exists is configuration: set BOOTSTRAP_ADMIN_EMAIL and
    // BOOTSTRAP_ADMIN_PASSWORD (optionally BOOTSTRAP_ADMIN_NAME) on the host. Unset, this does
    // nothing; set with the account already present, it only makes sure the role is attached.
    public static async Task SeedAdminFromConfigurationAsync(
        IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["BOOTSTRAP_ADMIN_EMAIL"];
        var password = configuration["BOOTSTRAP_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await userManager.FindByEmailAsync(email);

        if (existing != null)
        {
            // An account that signs in but holds no role lands on the public page with nothing
            // on it, which reads as a broken deploy rather than a missing role. Repair it.
            if (!await userManager.IsInRoleAsync(existing, AdminRole))
            {
                var repair = await userManager.AddToRoleAsync(existing, AdminRole);

                logger.LogInformation("Bootstrap admin {Email}: role Admin {Outcome}", email,
                    repair.Succeeded ? "granted" : string.Join(" ", repair.Errors.Select(e => e.Description)));
            }

            // The password is deliberately left alone: re-deploying should not silently reset
            // the credentials of a live account.
            return;
        }

        var failure = await CreateAdminAsync(
            userManager, email, password, configuration["BOOTSTRAP_ADMIN_NAME"] ?? email);

        if (failure != null)
            logger.LogError("Bootstrap admin {Email} not created: {Failure}", email, failure);
        else
            logger.LogInformation("Bootstrap admin {Email} created.", email);
    }
}
