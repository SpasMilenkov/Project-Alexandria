using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Models;
using Common.Seeding;
using Microsoft.Extensions.Configuration;

namespace Data.Seeders;

public class AdminSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ILogger<AdminSeeder> logger,
    IConfiguration configuration) : ISeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync(ct);
        await SeedAdminUserAsync(ct);
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        string[] roles = [Roles.Admin, Roles.User];

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var role = new ApplicationRole
            {
                Name = roleName,
            };

            var result = await roleManager.CreateAsync(role);

            if (result.Succeeded)
                logger.LogInformation("Role '{Role}' created successfully.", roleName);
            else
                logger.LogError("Failed to create role '{Role}': {Errors}",
                    roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedAdminUserAsync(CancellationToken ct)
    {
        var adminEmail = configuration.GetValue<string>("Admin:Email") ?? throw new InvalidOperationException("Configuration misses admin email");
        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing != null)
        {
            logger.LogInformation("Admin user already exists, skipping.");
            return;
        }

        var adminName = configuration.GetValue<string>("Admin:Name") ?? throw new InvalidOperationException("Configuration misses admin name");

        var admin = new ApplicationUser
        {
            Name = adminName,
            Email = adminEmail,
            UserName = adminName,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(admin, configuration.GetValue<string>("Admin:Password") ?? throw new InvalidOperationException("Configuration misses admin password"));
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Failed to assign Admin role to admin user: {Errors}",
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Admin user seeded successfully.");
    }
}
