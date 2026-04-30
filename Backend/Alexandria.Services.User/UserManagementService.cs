using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Seeding;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files;
using Alexandria.Dto.Users;
using Microsoft.AspNetCore.Identity;

namespace Alexandria.Services.User;

public class UserManagementService(UserManager<ApplicationUser> userManager, IUnitOfWork unitOfWork)
    : IUserManagementService
{
    public async Task DeleteUsersAsync(Guid[] userIds, CancellationToken ct = default)
    {
        await unitOfWork.Users.DeleteUsersAsync(userIds, ct);
    }

    public async Task<PaginatedResult<UserDetailsDto>> GetUsersAsync(UserQueryDto query, CancellationToken ct = default)
    {
        return await unitOfWork.Users.GetUsersAsync(query, ct);
    }

    public async Task RestrictUserAsync(Guid userId, DateTime restrictionEndDate, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new KeyNotFoundException($"User {userId} not found.");

        user.LockoutStartedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        var enableResult = await userManager.SetLockoutEnabledAsync(user, true);
        ThrowIfFailed(enableResult);

        var lockResult = await userManager.SetLockoutEndDateAsync(user, restrictionEndDate.ToUniversalTime());
        ThrowIfFailed(lockResult);
    }


    public async Task<UserDetailsDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!string.IsNullOrWhiteSpace(dto.UserName))
            user.UserName = dto.UserName;

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            user.Email = dto.Email;
            user.NormalizedEmail = userManager.NormalizeEmail(dto.Email);
        }

        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        ThrowIfFailed(updateResult);

        // Role swap — remove all current, assign new
        if (dto.Role.HasValue)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            ThrowIfFailed(removeResult);

            var addResult = await userManager.AddToRoleAsync(user, dto.Role.Value.ToString());
            ThrowIfFailed(addResult);
        }

        var assignedRole = (await userManager.GetRolesAsync(user)).FirstOrDefault();

        return new UserDetailsDto
        {
            Id = user.Id,
            UserName = user.UserName!,
            Email = user.Email!,
            IsLockedOut = await userManager.IsLockedOutAsync(user),
            Role = assignedRole != null ? Enum.Parse<UserRole>(assignedRole) : null,
            LockedOutStarted = user.LockoutStartedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            DeletedAt = user.DeletedAt,
        };
    }


    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    public async Task<UserDetailsDto> CreateUserAsync(string userName, string email, string password, UserRole userRole,
        CancellationToken ct = default)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            throw new UserCreationException("User with this email already exists", new()
            {
                ["Email"] = ["Email is already registered"]
            });
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow,
            Name = userName
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code.Contains("Password") ? "Password" :
                    e.Code.Contains("Email") ? "Email" :
                    e.Code.Contains("UserName") ? "UserName" : "General")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToList());

            throw new UserCreationException("Registration failed", errors);
        }

        var roleResult = await userManager.AddToRoleAsync(user, userRole == UserRole.Admin ? Roles.Admin : Roles.User);

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            throw new UserCreationException("Registration failed during role assignment",
                roleResult.Errors.ToDictionary(e => e.Code, e => new List<string> { e.Description }));
        }

        return user.ToDto();
    }

    public async Task<UserProfileDto> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
    {
        return await unitOfWork.Users.GetUserProfileAsync(userId, ct) ??
               throw new InvalidOperationException("User not found exception");
    }

    public async Task SetupProfileAsync(Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.Users.SetupProfileAsync(userId, ct);
    }

    public async Task FinishTourAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            var user = await unitOfWork.Users.GetByIdAsync(userId, ct) ??
                       throw new InvalidOperationException("User not found exception");

            user.OnboardinStep = OnboardingStep.Done;
            unitOfWork.Users.Update(user);

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
        }
    }

    public async Task<OnboardingStep?> GetOnboardingStepAsync(Guid userId, CancellationToken ct)
    {
        return await unitOfWork.Users.GetOnboardingStatusAsync(userId, ct);
    }
}