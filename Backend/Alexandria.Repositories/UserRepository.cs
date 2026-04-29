using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class UserRepository(AlexandriaDbContext context, UserManager<ApplicationUser> userManager) : IUserRepository
{
    public async Task<ApplicationUser> AddAsync(ApplicationUser entity, CancellationToken ct = default)
    {
        var result = await userManager.CreateAsync(entity);
        ThrowIfFailed(result);
        return entity;
    }

    public async Task<IEnumerable<ApplicationUser>> AddRangeAsync(IEnumerable<ApplicationUser> entities,
        CancellationToken ct = default)
    {
        var list = entities.ToList();
        foreach (var user in list)
        {
            var result = await userManager.CreateAsync(user);
            ThrowIfFailed(result);
        }

        return list;
    }

    public async Task<int> CountAsync(Expression<Func<ApplicationUser, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        return predicate is null
            ? await context.Users.CountAsync(ct)
            : await context.Users.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<ApplicationUser, bool>> predicate,
        CancellationToken ct = default)
    {
        return await context.Users.AnyAsync(predicate, ct);
    }

    public async Task<IEnumerable<ApplicationUser>> FindAsync(Expression<Func<ApplicationUser, bool>> predicate,
        CancellationToken ct = default)
    {
        return await context.Users.Where(predicate).AsNoTracking().ToListAsync(ct);
    }

    public async Task<ApplicationUser?> FirstOrDefaultAsync(Expression<Func<ApplicationUser, bool>> predicate,
        CancellationToken ct = default)
    {
        return await context.Users.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Users.AsNoTracking().ToListAsync(ct);
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await userManager.FindByIdAsync(id.ToString());
    }

    public void Update(ApplicationUser entity)
    {
        context.Users.Update(entity);
    }

    public void Remove(ApplicationUser entity)
    {
        var now = DateTime.UtcNow;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        context.Users.Update(entity);
    }

    public async Task DeleteUsers(Guid[] userIds, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await context.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.DeletedAt, now)
                    .SetProperty(u => u.UpdatedAt, now),
                ct);
    }

    public async Task<PaginatedResult<UserDetailsDto>> GetUsers(UserQueryDto query, CancellationToken ct = default)
    {
        var dbQuery =
            from user in context.Users
            join ur in context.UserRoles on user.Id equals ur.UserId into userRoles
            from ur in userRoles.DefaultIfEmpty()
            join role in context.Roles on ur.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new { user, RoleName = role.Name };

        // Deletion
        if (query.ShowDeletedOnly)
            dbQuery = dbQuery.Where(x => x.user.DeletedAt != null);
        else if (!query.ShowDeleted)
            dbQuery = dbQuery.Where(x => x.user.DeletedAt == null);

        // Text search
        if (!string.IsNullOrWhiteSpace(query.UserName))
            dbQuery = dbQuery.Where(x => x.user.UserName!.Contains(query.UserName));

        if (!string.IsNullOrWhiteSpace(query.UserEmail))
            dbQuery = dbQuery.Where(x => x.user.Email!.Contains(query.UserEmail));

        // Role
        if (query.Role.HasValue)
        {
            var roleName = query.Role.Value.ToString();
            dbQuery = dbQuery.Where(x => x.RoleName == roleName);
        }

        // Lockout
        if (query.IsLockedOut.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            dbQuery = query.IsLockedOut.Value
                ? dbQuery.Where(x => x.user.LockoutEnd != null && x.user.LockoutEnd > now)
                : dbQuery.Where(x => x.user.LockoutEnd == null || x.user.LockoutEnd <= now);
        }

        // Created
        if (query.CreatedAfter.HasValue)
            dbQuery = dbQuery.Where(x => x.user.CreatedAt >= query.CreatedAfter.Value.ToUniversalTime());
        if (query.CreatedBefore.HasValue)
            dbQuery = dbQuery.Where(x =>
                x.user.CreatedAt < query.CreatedBefore.Value.Date.AddDays(1).ToUniversalTime());

        // Updated
        if (query.UpdatedAfter.HasValue)
            dbQuery = dbQuery.Where(x => x.user.UpdatedAt >= query.UpdatedAfter.Value.ToUniversalTime());
        if (query.UpdatedBefore.HasValue)
            dbQuery = dbQuery.Where(x =>
                x.user.UpdatedAt < query.UpdatedBefore.Value.Date.AddDays(1).ToUniversalTime());

        // Deleted
        if (query.DeletedAfter.HasValue)
            dbQuery = dbQuery.Where(x => x.user.DeletedAt >= query.DeletedAfter.Value.ToUniversalTime());
        if (query.DeletedBefore.HasValue)
            dbQuery = dbQuery.Where(x =>
                x.user.DeletedAt < query.DeletedBefore.Value.Date.AddDays(1).ToUniversalTime());

        // Locked out range — uses LockoutStartedAt to filter when the restriction began
        if (query.LockedOutAfter.HasValue)
            dbQuery = dbQuery.Where(x => x.user.LockoutStartedAt >= query.LockedOutAfter.Value.ToUniversalTime());
        if (query.LockedOutBefore.HasValue)
            dbQuery = dbQuery.Where(x =>
                x.user.LockoutStartedAt < query.LockedOutBefore.Value.Date.AddDays(1).ToUniversalTime());


        dbQuery = query.SortBy switch
        {
            SortBy.Name => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(x => x.user.UserName)
                : dbQuery.OrderByDescending(x => x.user.UserName),

            SortBy.CreatedAt => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(x => x.user.CreatedAt)
                : dbQuery.OrderByDescending(x => x.user.CreatedAt),

            SortBy.UpdatedAt => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(x => x.user.UpdatedAt)
                : dbQuery.OrderByDescending(x => x.user.UpdatedAt),

            _ => dbQuery.OrderBy(x => x.user.UserName)
        };

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .AsNoTracking()
            .Skip(query.Page * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new UserDetailsDto
            {
                Id = x.user.Id,
                UserName = x.user.UserName!,
                Email = x.user.Email!,
                IsLockedOut = x.user.LockoutEnd != null && x.user.LockoutEnd > DateTimeOffset.UtcNow,
                Role = x.RoleName != null ? Enum.Parse<UserRole>(x.RoleName) : null,
                LockedOutStarted = x.user.LockoutStartedAt,
                CreatedAt = x.user.CreatedAt,
                UpdatedAt = x.user.UpdatedAt,
                DeletedAt = x.user.DeletedAt,
            })
            .ToListAsync(ct);

        return new PaginatedResult<UserDetailsDto>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
    }


    public void RemoveRange(IEnumerable<ApplicationUser> entities)
    {
        var now = DateTime.UtcNow;
        foreach (var user in entities)
        {
            user.DeletedAt = now;
            user.UpdatedAt = now;
        }

        context.Users.UpdateRange(entities);
    }

    public async Task<UserProfileDto?> GetUserProfile(Guid userId, CancellationToken ct = default)
    {
        return await context.Users.Where(u => u.Id == userId && u.DeletedAt == null)
            .Select(u => new UserProfileDto(u.UserName, u.Email, u.CreatedAt)).FirstOrDefaultAsync(ct);
    }

    public async Task SetupProfile(Guid userId, CancellationToken ct = default)
    {
        await context.Users.Where(u => u.Id == userId && u.DeletedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(u => u.OnboardinStep, OnboardingStep.Tour), ct);
    }

    public async Task<OnboardingStep?> GetOnboardingStatus(Guid userId, CancellationToken ct)
    {
        var result = await context.Users.Where(u => userId == u.Id && u.DeletedAt == null).Select(u => u.OnboardinStep)
            .FirstOrDefaultAsync(ct);
        return result;
    }
}