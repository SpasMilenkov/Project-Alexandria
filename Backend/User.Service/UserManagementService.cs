using Common;
using Common.Exceptions;
using Common.Services;
using DTO.Extensions;
using DTO.Files;
using DTO.Users;
using Microsoft.AspNetCore.Identity;
using Models;
using Models.Enumerators;

namespace User.Service;

public class UserManagementService(UserManager<ApplicationUser> userManager, IUnitOfWork unitOfWork) : IUserManagementService
{
    public async Task DeleteUsers(Guid[] userIds, CancellationToken ct = default)
    {
        await unitOfWork.Users.DeleteUsers(userIds, ct);
    }

    public async Task<PaginatedResult<UserDetailsDto>> GetUsers(UserQueryDto query, CancellationToken ct = default)
    {
        return await unitOfWork.Users.GetUsers(query, ct);
    }

    public async Task RestrictUser(Guid userId, DateTime restrictionEndDate, CancellationToken ct = default)
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


    public async Task<UserDetailsDto> UpdateUser(Guid userId, UpdateUserDto dto, CancellationToken ct = default)
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

    public async Task<UserDetailsDto> CreateUser(string userName, string email, string password, UserRole userRole, CancellationToken ct = default)
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

        var roleResult = await userManager.AddToRoleAsync(user, userRole == UserRole.Admin ? Common.Seeding.Roles.Admin : Common.Seeding.Roles.User);

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            throw new UserCreationException("Registration failed during role assignment",
                roleResult.Errors.ToDictionary(e => e.Code, e => new List<string> { e.Description }));
        }

        return user.ToDto();
    }

    public async Task<UserProfileDto> GetUserProfile(Guid userId, CancellationToken ct = default)
    {
        return await unitOfWork.Users.GetUserProfile(userId, ct) ?? throw new InvalidOperationException("User not found exception");
    }

    public async Task SetupProfile(Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.Users.SetupProfile(userId, ct);
    }

    public async Task FinishTour(Guid userId, CancellationToken ct = default)
    {
        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            var user = await unitOfWork.Users.GetByIdAsync(userId, ct) ?? throw new InvalidOperationException("User not found exception");

            user.OnboardinStep = OnboardingStep.Done;
            unitOfWork.Users.Update(user);

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
        }
    }

    public async Task<OnboardingStep?> GetOnboardingStep(Guid userId, CancellationToken ct)
    {
        return await unitOfWork.Users.GetOnboardingStatus(userId, ct);
    }
}
