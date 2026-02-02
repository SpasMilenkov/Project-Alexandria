using System.Linq.Expressions;
using Common.Repositories;
using Data.Context;
using DTO.Tags;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class TagRepository(AlexandriaDbContext context) : ITagRepository
{
    private readonly DbSet<Tag> _tags = context.Tags;

    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _tags.FindAsync(new object[] { id }, ct);
    }

    public async Task<Tag?> FirstOrDefaultAsync(Expression<Func<Tag, bool>> predicate, CancellationToken ct = default)
    {
        return await _tags.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<Tag>> GetAllAsync(CancellationToken ct = default)
    {
        return await _tags.ToListAsync(ct);
    }

    public async Task<IEnumerable<Tag>> FindAsync(Expression<Func<Tag, bool>> predicate, CancellationToken ct = default)
    {
        return await _tags.Where(predicate).ToListAsync(ct);
    }

    public async Task<Tag> AddAsync(Tag entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var entry = await _tags.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<Tag>> AddRangeAsync(IEnumerable<Tag> entities, CancellationToken ct = default)
    {
        var tagList = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in tagList)
        {
            entity.CreatedAt = now;
        }

        await _tags.AddRangeAsync(tagList, ct);
        return tagList;
    }

    public void Update(Tag entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _tags.Update(entity);
    }

    public void Remove(Tag entity)
    {
        _tags.Remove(entity);
    }

    public void RemoveRange(IEnumerable<Tag> entities)
    {
        _tags.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<Tag, bool>>? predicate = null, CancellationToken ct = default)
    {
        return predicate == null
            ? await _tags.CountAsync(ct)
            : await _tags.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<Tag, bool>> predicate, CancellationToken ct = default)
    {
        return await _tags.AnyAsync(predicate, ct);
    }

    public async Task<Tag> CreateAsync(Tag tag, CancellationToken ct = default)
    {
        tag.CreatedAt = DateTime.UtcNow;
        var entry = await _tags.AddAsync(tag, ct);
        await context.SaveChangesAsync(ct);
        return entry.Entity;
    }

    public async Task<Tag> UpdateAsync(Tag tag, CancellationToken ct = default)
    {
        tag.UpdatedAt = DateTime.UtcNow;
        _tags.Update(tag);
        await context.SaveChangesAsync(ct);
        return tag;
    }

    // Additional methods specific to Tags
    public async Task<Tag?> GetByIdAndUserIdAsync(Guid tagId, Guid userId, CancellationToken ct = default)
    {
        return await _tags.FirstOrDefaultAsync(t => t.Id == tagId && t.OwnerId == userId && t.DeletedAt == null, ct);
    }

    public async Task<Tag?> GetByNameAndUserIdAsync(string name, Guid userId, CancellationToken ct = default)
    {
        return await _tags.FirstOrDefaultAsync(t => t.Name == name && t.OwnerId == userId, ct);
    }

    public async Task<IEnumerable<TagDto>> GetTagsWithFilesAsync(Guid userId, CancellationToken ct = default)
    {
        return await _tags
            .Include(t => t.Files)
            .Where(t => t.OwnerId == userId)
            .Select(t => new TagDto
            {
                Id = t.Id,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                Name = t.Name,
                Color = t.Color,
                Icon = t.Icon,
                Description = t.Description,
                UserId = t.OwnerId
            })
            .ToListAsync(ct);
    }

    public async Task<(IEnumerable<TagDto> Tags, int TotalCount)> FindTagsAsync(
        TagSearchQuery query,
        CancellationToken ct = default)
    {
        IQueryable<Tag> tagsQuery = _tags.Where(t => t.DeletedAt == null);

        // Only include Files if we need to filter by HasFiles
        if (query.HasFiles.HasValue)
        {
            tagsQuery = _tags.Include(t => t.Files).Where(t => t.DeletedAt == null);
        }

        // Apply user ID filter
        if (query.UserId.HasValue)
        {
            tagsQuery = tagsQuery.Where(t => t.OwnerId == query.UserId.Value);
        }

        // Apply created by filter
        if (query.CreatedBy.HasValue)
        {
            tagsQuery = tagsQuery.Where(t => t.OwnerId == query.CreatedBy.Value);
        }

        // Apply updated by filter
        if (query.UpdatedBy.HasValue)
        {
            tagsQuery = tagsQuery.Where(t => t.UpdatedBy == query.UpdatedBy);
        }

        // Apply creation date filters
        if (query.CreatedAfter.HasValue)
        {
            tagsQuery = tagsQuery.Where(t => t.CreatedAt >= query.CreatedAfter.Value);
        }

        if (query.CreatedBefore.HasValue)
        {
            tagsQuery = tagsQuery.Where(t => t.CreatedAt <= query.CreatedBefore.Value);
        }

        // Apply update date filters
        if (query.UpdatedAfter.HasValue && query.UpdatedAfter.Value != default)
        {
            tagsQuery = tagsQuery.Where(t =>
                t.UpdatedAt.HasValue && t.UpdatedAt.Value >= query.UpdatedAfter.Value);
        }

        if (query.UpdatedBefore.HasValue && query.UpdatedBefore.Value != default)
        {
            tagsQuery = tagsQuery.Where(t =>
                t.UpdatedAt.HasValue && t.UpdatedAt.Value <= query.UpdatedBefore.Value);
        }

        // Apply name search filter
        if (!string.IsNullOrWhiteSpace(query.NameContains))
        {
            tagsQuery = tagsQuery.Where(d => d.Name.Contains(query.NameContains));
        }

        // Apply has files filter
        if (query.HasFiles.HasValue)
        {
            if (query.HasFiles.Value)
            {
                tagsQuery = tagsQuery.Where(t => t.Files != null && t.Files.Any());
            }
            else
            {
                tagsQuery = tagsQuery.Where(t => t.Files == null || !t.Files.Any());
            }
        }

        var totalCount = await tagsQuery.CountAsync(ct);

        var tags = await tagsQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip((query.CurrentPage - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TagDto
            {
                Id = t.Id,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                Name = t.Name,
                Color = t.Color,
                Icon = t.Icon,
                Description = t.Description,
                UserId = t.OwnerId
            })
            .ToListAsync(ct);

        return (tags, totalCount);
    }
}