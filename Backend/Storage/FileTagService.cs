using Common;
using DTO;
using Microsoft.Extensions.Logging;
using Models;
using File = Models.File;

namespace Storage;

public class FileTagService(
    IUnitOfWork unitOfWork,
    ILogger<FileTagService> logger) : IFileTagService
{
    public async Task<Tag> CreateAsync(string name, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty", nameof(name));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        var existingTag = await unitOfWork.Tags.GetByNameAndUserIdAsync(name, userId, ct);
        if (existingTag != null)
        {
            logger.LogWarning("Tag {TagName} already exists for user {UserId}", name, userId);
            throw new InvalidOperationException($"Tag '{name}' already exists for this user");
        }

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);
            var createdTag = await unitOfWork.Tags.CreateAsync(tag, ct);
            await unitOfWork.CommitAsync(ct);
            
            logger.LogInformation("Tag {TagId} created successfully by user {UserId}", createdTag.Id, userId);
            return createdTag;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            logger.LogError(ex, "Error creating tag {TagName} for user {UserId}", name, userId);
            throw;
        }
    }

    public async Task<PaginatedResult<Tag>> GetTagsAsync(
        int currentPage = 0, 
        int pageSize = 20, 
        CancellationToken ct = default)
    {
        if (currentPage < 0)
            throw new ArgumentException("Page number must be non-negative", nameof(currentPage));

        if (pageSize <= 0 || pageSize > 100)
            throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

        try
        {
            var totalCount = await unitOfWork.Tags.CountAsync(t => t.DeletedAt == null, ct);
            
            var tags = await unitOfWork.Tags.FindAsync(
                t => t.DeletedAt == null, 
                ct);

            var paginatedTags = tags
                .OrderByDescending(t => t.CreatedAt)
                .Skip(currentPage * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<Tag>
            {
                Items = paginatedTags,
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving tags");
            throw;
        }
    }

    public async Task<Tag> UpdateTagAsync(
        Guid tagId,
        string name, 
        Guid userId, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty", nameof(name));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        var tag = await unitOfWork.Tags.GetByIdAsync(tagId, ct);
        
        if (tag == null || tag.DeletedAt != null)
            throw new InvalidOperationException($"Tag {tagId} not found");

        if (tag.UserId != userId)
            throw new UnauthorizedAccessException("You do not have permission to update this tag");

        if (tag.Name != name.Trim())
        {
            var existingTag = await unitOfWork.Tags.GetByNameAndUserIdAsync(name, userId, ct);
            if (existingTag != null && existingTag.Id != tagId)
                throw new InvalidOperationException($"Tag '{name}' already exists for this user");
        }

        tag.Name = name.Trim();
        tag.UpdatedAt = DateTime.UtcNow;
        tag.UpdatedBy = userId.ToString();

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);
            var updatedTag = await unitOfWork.Tags.UpdateAsync(tag, ct);
            await unitOfWork.CommitAsync(ct);
            
            logger.LogInformation("Tag {TagId} updated successfully", tagId);
            return updatedTag;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            logger.LogError(ex, "Error updating tag {TagId}", tagId);
            throw;
        }
    }

    public async Task DeleteTagAsync(Guid tagId, Guid userId, CancellationToken ct = default)
    {
        var tag = await unitOfWork.Tags.GetByIdAsync(tagId, ct);
        
        if (tag is not { DeletedAt: null })
            throw new InvalidOperationException($"Tag {tagId} not found");

        if (tag.UserId != userId)
            throw new UnauthorizedAccessException("You do not have permission to delete this tag");

        tag.DeletedAt = DateTime.UtcNow;
        tag.UpdatedBy = userId.ToString();

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);
            unitOfWork.Tags.Update(tag);
            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);
            
            logger.LogInformation("Tag {TagId} soft-deleted successfully", tagId);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            logger.LogError(ex, "Error deleting tag {TagId}", tagId);
            throw;
        }
    }

    public async Task AddTagsToFileAsync(
        Guid fileId, 
        ICollection<Guid> tagIds, 
        Guid userId,
        CancellationToken ct = default)
    {
        if (tagIds == null || !tagIds.Any())
            throw new ArgumentException("Tag IDs cannot be empty", nameof(tagIds));

        var file = await unitOfWork.Files.GetByIdAsync(fileId, ct);
        
        if (file == null || file.DeletedAt != null)
            throw new InvalidOperationException($"File {fileId} not found");

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            foreach (var tagId in tagIds)
            {
                var tag = await unitOfWork.Tags.GetByIdAsync(tagId, ct);
                
                if (tag == null || tag.DeletedAt != null)
                {
                    logger.LogWarning("Tag {TagId} not found, skipping", tagId);
                    continue;
                }

                if (tag.UserId != userId)
                {
                    logger.LogWarning("User {UserId} does not own tag {TagId}, skipping", userId, tagId);
                    continue;
                }

                if (file.Tags.Any(t => t.Id == tagId))
                {
                    logger.LogDebug("File {FileId} already has tag {TagId}, skipping", fileId, tagId);
                    continue;
                }

                file.Tags.Add(tag);
            }

            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);
            
            logger.LogInformation("Added {Count} tags to file {FileId}", tagIds.Count, fileId);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            logger.LogError(ex, "Error adding tags to file {FileId}", fileId);
            throw;
        }
    }

    public async Task RemoveTagFromFileAsync(
        Guid fileId, 
        Guid tagId, 
        Guid userId,
        CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.GetByIdAsync(fileId, ct);
        
        if (file == null || file.DeletedAt != null)
            throw new InvalidOperationException($"File {fileId} not found");

        var tag = file.Tags.FirstOrDefault(t => t.Id == tagId);
        
        if (tag == null)
        {
            logger.LogWarning("Tag {TagId} not associated with file {FileId}", tagId, fileId);
            return;
        }

        if (tag.UserId != userId)
            throw new UnauthorizedAccessException("You do not have permission to remove this tag");

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);
            file.Tags.Remove(tag);
            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);
            
            logger.LogInformation("Removed tag {TagId} from file {FileId}", tagId, fileId);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            logger.LogError(ex, "Error removing tag {TagId} from file {FileId}", tagId, fileId);
            throw;
        }
    }

    public async Task<PaginatedResult<File>> FindFilesByTagsAsync(
        FileTagSearchQuery query,
        CancellationToken ct = default)
    {
        if (query.TagIds == null || !query.TagIds.Any())
            throw new ArgumentException("At least one tag ID must be provided", nameof(query.TagIds));

        if (query.CurrentPage < 0)
            throw new ArgumentException("Page number must be non-negative", nameof(query.CurrentPage));

        if (query.PageSize <= 0 || query.PageSize > 100)
            throw new ArgumentException("Page size must be between 1 and 100", nameof(query.PageSize));

        try
        {
            var (files, totalCount) = await unitOfWork.Files.FindFilesByTagsAsync(query, ct);

            return new PaginatedResult<File>
            {
                Items = files.ToList(),
                CurrentPage = query.CurrentPage,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding files by tags");
            throw;
        }
    }

    public async Task<ICollection<Tag>> GetTagsForFileAsync(
        Guid fileId, 
        CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.GetFileWithTagsAsync(fileId, ct);
        
        if (file == null || file.DeletedAt != null)
            throw new InvalidOperationException($"File {fileId} not found");

        return file.Tags
            .Where(t => t.DeletedAt == null)
            .OrderBy(t => t.Name)
            .ToList();
    }

    public async Task<PaginatedResult<Tag>> FindTagsAsync(
        TagSearchQuery query,
        CancellationToken ct = default)
    {
        if (query.CurrentPage < 0)
            throw new ArgumentException("Page number must be non-negative", nameof(query.CurrentPage));

        if (query.PageSize <= 0 || query.PageSize > 100)
            throw new ArgumentException("Page size must be between 1 and 100", nameof(query.PageSize));

        try
        {
            var (tags, totalCount) = await unitOfWork.Tags.FindTagsAsync(query, ct);

            return new PaginatedResult<Tag>
            {
                Items = tags.ToList(),
                CurrentPage = query.CurrentPage,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding tags with criteria");
            throw;
        }
    }
}