using Common;
using Common.Services;
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
}
