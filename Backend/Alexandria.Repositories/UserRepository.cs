using System.Linq.Expressions;
using Alexandria.Common.Config;
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

    public async Task DeleteUsersAsync(Guid[] userIds, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await context.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.DeletedAt, now)
                    .SetProperty(u => u.UpdatedAt, now),
                ct);
    }

    public async Task<(long TotalQuotaBytes, int UserCount)> GetTotalStorageQuotaAsync(
        CancellationToken ct = default)
    {
        var live = context.Users.AsNoTracking().Where(u => u.DeletedAt == null);
        var total = await live.SumAsync(u => (long?)u.StorageQuota, ct) ?? 0;
        var count = await live.CountAsync(ct);
        return (total, count);
    }

    public async Task<IReadOnlyList<AccountStateRow>> GetAccountStatesAsync(CancellationToken ct = default)
    {
        var systemId = SystemConfig.SystemId;

        return await context.Users
            .AsNoTracking()
            .Where(u => u.DeletedAt == null && u.Id != systemId)
            .OrderBy(u => u.Name)
            .Select(u => new AccountStateRow(u.Id, u.Name, u.Email ?? string.Empty, u.LockoutEnd))
            .ToListAsync(ct);
    }

    internal sealed class UserListRow
    {
        public required ApplicationUser User { get; init; }
        public string? RoleName { get; init; }
    }

    private const string LikeEscape = "\\";

    // Usernames can contain underscores, which LIKE would otherwise treat as a wildcard.
    internal static string ContainsPattern(string term)
    {
        var escaped = term.Trim()
            .Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");

        return $"%{escaped}%";
    }

    internal static IQueryable<UserListRow> BuildUserListQuery(AlexandriaDbContext context, UserQueryDto query)
    {
        var dbQuery =
            from user in context.Users
            join ur in context.UserRoles on user.Id equals ur.UserId into userRoles
            from ur in userRoles.DefaultIfEmpty()
            join role in context.Roles on ur.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new UserListRow { User = user, RoleName = role.Name };

        // Deletion
        if (query.ShowDeletedOnly)
            dbQuery = dbQuery.Where(x => x.User.DeletedAt != null);
        else if (!query.ShowDeleted)
            dbQuery = dbQuery.Where(x => x.User.DeletedAt == null);

        // Text search
        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            var namePattern = ContainsPattern(query.UserName);
            dbQuery = dbQuery.Where(x => EF.Functions.ILike(x.User.UserName!, namePattern, LikeEscape));
        }

        if (!string.IsNullOrWhiteSpace(query.UserEmail))
        {
            var emailPattern = ContainsPattern(query.UserEmail);
            dbQuery = dbQuery.Where(x => EF.Functions.ILike(x.User.Email!, emailPattern, LikeEscape));
        }

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
                ? dbQuery.Where(x => x.User.LockoutEnd != null && x.User.LockoutEnd > now)
                : dbQuery.Where(x => x.User.LockoutEnd == null || x.User.LockoutEnd <= now);
        }

        // Created
        if (query.CreatedAfter.HasValue)
            dbQuery = dbQuery.Where(x => x.User.CreatedAt >= query.CreatedAfter.Value.ToUniversalTime());
        if (query.CreatedBefore.HasValue)
            dbQuery = dbQuery.Where(x =>
                x.User.CreatedAt < query.CreatedBefore.Value.Date.AddDays(1).ToUniversalTime());

        // Updated
        if (query.UpdatedAfter.HasValue)
            dbQuery = dbQuery.Where(x => x.User.UpdatedAt >= query.UpdatedAfter.Value.ToUniversalTime());
        if (query.UpdatedBefore.HasValue)
            dbQuery = dbQuery.Where(x =>
                x.User.UpdatedAt < query.UpdatedBefore.Value.Date.AddDays(1).ToUniversalTime());

        // Deleted
        if (query.DeletedAfter.HasValue)
            dbQuery = dbQuery.Where(x => x.User.DeletedAt >= query.DeletedAfter.Value.ToUniversalTime());
        if (query.DeletedBefore.HasValue)
            dbQuery = dbQuery.Where(x =>
                x.User.DeletedAt < query.DeletedBefore.Value.Date.AddDays(1).ToUniversalTime());

        // Locked out range — uses LockoutStartedAt to filter when the restriction began
        if (query.LockedOutAfter.HasValue)
            dbQuery = dbQuery.Where(x => x.User.LockoutStartedAt >= query.LockedOutAfter.Value.ToUniversalTime());
        if (query.LockedOutBefore.HasValue)
            dbQuery = dbQuery.Where(x =>
                x.User.LockoutStartedAt < query.LockedOutBefore.Value.Date.AddDays(1).ToUniversalTime());

        // The totals match what the expanded user row shows, so the list reads as sorted.
        return query.SortBy switch
        {
            SortBy.Name => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(x => x.User.UserName)
                : dbQuery.OrderByDescending(x => x.User.UserName),

            SortBy.CreatedAt => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(x => x.User.CreatedAt)
                : dbQuery.OrderByDescending(x => x.User.CreatedAt),

            SortBy.UpdatedAt => query.SortDirection == SortDirection.Asc
                ? dbQuery.OrderBy(x => x.User.UpdatedAt)
                : dbQuery.OrderByDescending(x => x.User.UpdatedAt),

            SortBy.FileCount => (query.SortDirection == SortDirection.Asc
                    ? dbQuery.OrderBy(x => context.Files.Count(f => f.OwnerId == x.User.Id))
                    : dbQuery.OrderByDescending(x => context.Files.Count(f => f.OwnerId == x.User.Id)))
                .ThenBy(x => x.User.UserName),

            SortBy.TotalSize => (query.SortDirection == SortDirection.Asc
                    ? dbQuery.OrderBy(x => context.FileVersions
                        .Where(v => v.File.OwnerId == x.User.Id && v.File.DeletedAt == null)
                        .Sum(v => (long?)v.Size) ?? 0)
                    : dbQuery.OrderByDescending(x => context.FileVersions
                        .Where(v => v.File.OwnerId == x.User.Id && v.File.DeletedAt == null)
                        .Sum(v => (long?)v.Size) ?? 0))
                .ThenBy(x => x.User.UserName),

            _ => dbQuery.OrderBy(x => x.User.UserName)
        };
    }

    public async Task<PaginatedResult<UserDetailsDto>> GetUsersAsync(UserQueryDto query, CancellationToken ct = default)
    {
        var dbQuery = BuildUserListQuery(context, query);

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .AsNoTracking()
            .Skip(query.Page * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new UserDetailsDto
            {
                Id = x.User.Id,
                UserName = x.User.UserName!,
                Email = x.User.Email!,
                IsLockedOut = x.User.LockoutEnd != null && x.User.LockoutEnd > DateTimeOffset.UtcNow,
                Role = x.RoleName != null ? Enum.Parse<UserRole>(x.RoleName) : null,
                LockedOutStarted = x.User.LockoutStartedAt,
                CreatedAt = x.User.CreatedAt,
                UpdatedAt = x.User.UpdatedAt,
                DeletedAt = x.User.DeletedAt,
                StorageQuota = x.User.StorageQuota,
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
        var applicationUsers = entities as ApplicationUser[] ?? entities.ToArray();
        foreach (var user in applicationUsers)
        {
            user.DeletedAt = now;
            user.UpdatedAt = now;
        }

        context.Users.UpdateRange(applicationUsers);
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.Users.Where(u => u.Id == userId && u.DeletedAt == null)
            .Select(u => new UserProfileDto(u.UserName, u.Email, u.CreatedAt)).FirstOrDefaultAsync(ct);
    }

    public async Task SetupProfileAsync(Guid userId, CancellationToken ct = default)
    {
        await context.Users.Where(u => u.Id == userId && u.DeletedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(u => u.OnboardinStep, OnboardingStep.Tour), ct);
    }

    public async Task<OnboardingStep?> GetOnboardingStatusAsync(Guid userId, CancellationToken ct)
    {
        var result = await context.Users.Where(u => userId == u.Id && u.DeletedAt == null).Select(u => u.OnboardinStep)
            .FirstOrDefaultAsync(ct);
        return result;
    }
}