using Alexandria.Common.Config;
using Alexandria.Common.Seeding;
using Alexandria.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Alexandria.Data.Seeders;

public class AdminSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ILogger<AdminSeeder> logger,
    IConfiguration configuration) : ISeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await SeedRolesAsync();

        ct.ThrowIfCancellationRequested();
        await SeedAdminUserAsync(ct);

        ct.ThrowIfCancellationRequested();
        await SeedSystemUserAsync();
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = [Roles.Admin, Roles.User];

        foreach (var roleName in roles)
        {
            if (await ExecuteAsync(() => roleManager.RoleExistsAsync(roleName), "RoleLookupFailed"))
                continue;

            var role = new ApplicationRole
            {
                Name = roleName,
            };

            var result = await ExecuteAsync(() => roleManager.CreateAsync(role), "RoleCreationFailed");

            if (!result.Succeeded)
                throw Failure("RoleCreationFailed", result);

            logger.LogInformation("Role '{Role}' created successfully.", roleName);
        }
    }

    private async Task SeedAdminUserAsync(CancellationToken ct)
    {
        var adminEmail = RequiredSetting("Admin:Email");
        var existing = await ExecuteAsync(() => userManager.FindByEmailAsync(adminEmail), "AdminLookupFailed");
        if (existing != null)
        {
            if (existing.DeletedAt is not null || existing.Id == SystemConfig.SystemId)
                throw Failure("ExistingAdminUnavailable");

            if (!await ExecuteAsync(() => userManager.IsInRoleAsync(existing, Roles.Admin), "AdminRoleLookupFailed"))
                throw Failure("ExistingAccountIsNotAdmin", recovery:
                    "Resolve the configured account or its role through an authorized recovery process before retrying.");

            logger.LogInformation("Existing admin account verified; credentials preserved.");
            return;
        }

        var adminName = RequiredSetting("Admin:Name");
        var adminPassword = RequiredSetting("Admin:Password");

        var admin = new ApplicationUser
        {
            Name = adminName,
            Email = adminEmail,
            UserName = adminName,
            EmailConfirmed = true,
            StorageQuota = ApplicationUser.DefaultStorageQuotaBytes,
        };

        var createResult = await ExecuteAsync(() => userManager.CreateAsync(admin, adminPassword), "AdminCreationFailed");
        if (!createResult.Succeeded)
            throw Failure("AdminCreationFailed", createResult);

        try
        {
            ct.ThrowIfCancellationRequested();
            var roleResult = await ExecuteAsync(() => userManager.AddToRoleAsync(admin, Roles.Admin), "AdminRoleAssignmentFailed");
            if (!roleResult.Succeeded)
                throw Failure("AdminRoleAssignmentFailed", roleResult);

            var stored = await ExecuteAsync(() => userManager.FindByEmailAsync(adminEmail), "AdminVerificationFailed");
            if (stored is null || stored.Id != admin.Id || stored.DeletedAt is not null ||
                !await ExecuteAsync(() => userManager.CheckPasswordAsync(stored, adminPassword), "AdminVerificationFailed") ||
                !await ExecuteAsync(() => userManager.IsInRoleAsync(stored, Roles.Admin), "AdminVerificationFailed"))
                throw Failure("AdminVerificationFailed");
        }
        catch
        {
            await RollbackNewAdminAsync(admin);
            throw;
        }

        logger.LogInformation("Admin user seeded successfully.");
    }

    private async Task RollbackNewAdminAsync(ApplicationUser admin)
    {
        const string recovery =
            "The newly created account remains incomplete; use an authorized recovery process before retrying.";

        // Only this attempt's newly created account may be removed during recovery.
        var rollback = await ExecuteAsync(() => userManager.DeleteAsync(admin), "AdminRollbackFailed", recovery);
        if (!rollback.Succeeded)
            throw Failure("AdminRollbackFailed", rollback, recovery);

        logger.LogWarning("Incomplete new admin account removed; bootstrap can be retried.");
    }

    private async Task SeedSystemUserAsync()
    {
        logger.LogInformation("Creating system account");

        var exists = await ExecuteAsync(() => userManager.FindByIdAsync(SystemConfig.SystemId.ToString()), "SystemAccountLookupFailed");

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

        var result = await ExecuteAsync(() => userManager.CreateAsync(system), "SystemAccountCreationFailed");

        if (!result.Succeeded)
            throw Failure("SystemAccountCreationFailed", result);

        logger.LogInformation("System user created successfully");
    }

    private string RequiredSetting(string key)
    {
        var value = configuration.GetValue<string>(key);
        if (string.IsNullOrWhiteSpace(value))
            throw Failure("AdminConfigurationMissing", recovery: $"Configure {key} before retrying.");

        return value;
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string failureCode, string? recovery = null)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Store exceptions and Identity descriptions can include credentials or account input.
            throw Failure(failureCode, recovery: recovery);
        }
    }

    private InvalidOperationException Failure(string code, IdentityResult? result = null, string? recovery = null)
    {
        var codes = result?.Errors.Select(error => SafeIdentityCode(error.Code)).Distinct().ToArray() ?? [];
        var message = $"Account bootstrap failed [{code}].";
        if (codes.Length > 0)
            message += $" Identity codes: {string.Join(", ", codes)}.";

        if (recovery is not null)
            message += $" {recovery}";

        logger.LogError("{BootstrapFailure}", message);
        return new InvalidOperationException(message);
    }

    private static string SafeIdentityCode(string code) => code switch
    {
        nameof(IdentityErrorDescriber.DefaultError) or
        nameof(IdentityErrorDescriber.ConcurrencyFailure) or
        nameof(IdentityErrorDescriber.PasswordMismatch) or
        nameof(IdentityErrorDescriber.InvalidToken) or
        nameof(IdentityErrorDescriber.LoginAlreadyAssociated) or
        nameof(IdentityErrorDescriber.InvalidUserName) or
        nameof(IdentityErrorDescriber.InvalidEmail) or
        nameof(IdentityErrorDescriber.DuplicateUserName) or
        nameof(IdentityErrorDescriber.DuplicateEmail) or
        nameof(IdentityErrorDescriber.InvalidRoleName) or
        nameof(IdentityErrorDescriber.DuplicateRoleName) or
        nameof(IdentityErrorDescriber.UserAlreadyHasPassword) or
        nameof(IdentityErrorDescriber.UserLockoutNotEnabled) or
        nameof(IdentityErrorDescriber.UserAlreadyInRole) or
        nameof(IdentityErrorDescriber.UserNotInRole) or
        nameof(IdentityErrorDescriber.PasswordTooShort) or
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) or
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) or
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) or
        nameof(IdentityErrorDescriber.PasswordRequiresLower) or
        nameof(IdentityErrorDescriber.PasswordRequiresUpper) => code,
        _ => "UnrecognizedIdentityError",
    };
}
