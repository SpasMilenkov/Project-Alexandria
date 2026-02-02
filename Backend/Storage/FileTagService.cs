using Common;
using Common.Services;
using DTO.Files;
using DTO.Tags;
using Microsoft.Extensions.Logging;
using Models;

namespace Storage;

public class FileTagService(
    IUnitOfWork unitOfWork,
    ILogger<FileTagService> logger) : IFileTagService
{
    public async Task<Tag> CreateAsync(string name,
        string color,
        string icon,
        string? description,
        Guid userId,
        CancellationToken ct = default)
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
            OwnerId = userId,
            Icon = icon,
            Color = color,
            Description = description,
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

    public async Task<Tag> UpdateTagAsync(Guid tagId,
        Guid userId,
        string? name,
        string? color,
        string? icon,
        string? description, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        var tag = await unitOfWork.Tags.GetByIdAndUserIdAsync(tagId, userId, ct);

        if (tag is null) throw new InvalidOperationException("Tag not found");

        if (tag.Name != name?.Trim())
        {
            var existingTag = await unitOfWork.Tags.GetByNameAndUserIdAsync(name, userId, ct);
            if (existingTag != null && existingTag.Id != tagId)
                throw new InvalidOperationException($"Tag '{name}' already exists for this user");
        }

        if (!string.IsNullOrEmpty(name))
            tag.Name = name.Trim();

        if (!string.IsNullOrEmpty(color))
            tag.Color = color.Trim();

        if (!string.IsNullOrEmpty(icon))
            tag.Icon = icon.Trim();

        //TODO: this might be a little destructive, ensuring the frontend always sends the description here is essential
        tag.Description = description;
        tag.UpdatedAt = DateTime.UtcNow;
        tag.UpdatedBy = userId;

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

        if (tag.OwnerId != userId)
            throw new UnauthorizedAccessException("You do not have permission to delete this tag");

        tag.DeletedAt = DateTime.UtcNow;
        tag.UpdatedBy = userId;

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
        if (tagIds == null || tagIds.Count == 0)
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

                if (tag is not { DeletedAt: null })
                {
                    logger.LogWarning("Tag {TagId} not found, skipping", tagId);
                    continue;
                }

                if (tag.OwnerId != userId)
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
        var file = await unitOfWork.Files.GetFileEntityWithTagsAsync(fileId, ct);

        if (file is not { DeletedAt: null })
            throw new InvalidOperationException($"File {fileId} not found");

        var tag = file.Tags.FirstOrDefault(t => t.Id == tagId);

        if (tag == null)
        {
            logger.LogWarning("Tag {TagId} not associated with file {FileId}", tagId, fileId);
            return;
        }

        if (tag.OwnerId != userId)
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

    public async Task<PaginatedResult<FileResult>> FindFilesByTagsAsync(FileTagSearchQuery query,
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
            return await unitOfWork.Files.FindFilesByTagsAsync(query, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding files by tags");
            throw;
        }
    }

    public async Task<ICollection<TagDto>> GetTagsForFileAsync(Guid fileId,
        CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.GetFileWithTagsAsync(fileId, ct);

        if (file is not { DeletedAt: null })
            throw new InvalidOperationException($"File {fileId} not found");

        return file.Tags;
    }

    public async Task<PaginatedResult<TagDto>> FindTagsAsync(
        TagSearchQuery query,
        CancellationToken ct = default)
    {
        if (query.CurrentPage < 0)
            throw new ArgumentException("Page number must be non-negative", nameof(query.CurrentPage));

        if (query.PageSize is <= 0 or > 100)
            throw new ArgumentException("Page size must be between 1 and 100", nameof(query.PageSize));

        try
        {
            var (tags, totalCount) = await unitOfWork.Tags.FindTagsAsync(query, ct);

            return new PaginatedResult<TagDto>
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