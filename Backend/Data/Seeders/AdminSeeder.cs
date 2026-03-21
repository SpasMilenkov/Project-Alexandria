using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Models;
using Common.Seeding;
using Microsoft.Extensions.Configuration;
using Common.Config;

namespace Data.Seeders;

public class AdminSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ILogger<AdminSeeder> logger,
    IConfiguration configuration) : ISeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync();
        await SeedAdminUserAsync();
        await SeedSystemUserAsync();
    }

    private async Task SeedRolesAsync()
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

    private async Task SeedAdminUserAsync()
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

    private async Task SeedSystemUserAsync()
    {

        logger.LogInformation("Creating system account");

        var exists = await userManager.FindByIdAsync(SystemConfig.SystemId.ToString());

        if (exists is not null)
        {
            logger.LogInformation("System user already exists");
            return;
        }

        var system = new ApplicationUser
        {
            Name = SystemConfig.SystemAccountName,
            UserName = SystemConfig.SystemAccountName,
            Id = SystemConfig.SystemId,
            Email = SystemConfig.SystemEmail,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(system);

        if (!result.Succeeded)
        {
            logger.LogError("Failed to seed system account: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("System user created successfully");
    }
}
