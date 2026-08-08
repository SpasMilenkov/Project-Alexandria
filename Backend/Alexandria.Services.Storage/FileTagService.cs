using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Tags;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage;

public partial class FileTagService(
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
            LogTagAlreadyExists(logger, name, userId);
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

            LogTagCreated(logger, createdTag.Id, userId);
            return createdTag;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogTagCreationFailed(logger, ex, name, userId);
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

        var tag = await unitOfWork.Tags.GetByIdAndUserIdAsync(userId, tagId, ct)
                  ?? throw new InvalidOperationException("Tag not found");

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

            LogTagUpdated(logger, tagId);
            return updatedTag;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogTagUpdateFailed(logger, ex, tagId);
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

            LogTagDeleted(logger, tagId);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogTagDeletionFailed(logger, ex, tagId);
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

        var file = await unitOfWork.Files.GetFileEntityWithTagsAsync(fileId, ct);

        if (file is not { DeletedAt: null })
            throw new InvalidOperationException($"File {fileId} not found");

        if (file.OwnerId != userId)
            throw new UnauthorizedAccessException("You do not have permission to tag this file");

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            var now = DateTime.UtcNow;

            foreach (var tagId in tagIds)
            {
                var tag = await unitOfWork.Tags.GetByIdAsync(tagId, ct);

                if (tag is not { DeletedAt: null })
                {
                    LogTagNotFoundSkipping(logger, tagId);
                    continue;
                }

                // Valid to associate: the user's own tag, or a shared system tag (genre/mood vocabulary)
                if (tag.OwnerId != userId && tag.OwnerId != SystemConfig.SystemId)
                {
                    LogTagOwnershipMismatchSkipping(logger, userId, tagId);
                    continue;
                }

                var existing = file.FileTags!.FirstOrDefault(ft => ft.TagId == tagId);

                if (existing is null)
                {
                    file.FileTags!.Add(new FileTag
                    {
                        FileId = fileId,
                        TagId = tagId,
                        Source = TagSource.User,
                        CreatedAt = now
                    });
                    continue;
                }

                if (existing.Source == TagSource.Suppressed)
                {
                    // User previously removed this tag, re-adding it explicitly overrides that
                    existing.Source = TagSource.User;
                    existing.Confidence = null;
                    existing.UpdatedAt = now;
                    continue;
                }

                LogFileAlreadyHasTagSkipping(logger, fileId, tagId);
            }

            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);

            LogTagsAddedToFile(logger, tagIds.Count, fileId);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogAddTagsToFileFailed(logger, ex, fileId);
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

        if (file.OwnerId != userId)
            throw new UnauthorizedAccessException("You do not have permission to remove this tag");

        var fileTag = file.FileTags!.FirstOrDefault(ft => ft.TagId == tagId && ft.Source != TagSource.Suppressed);

        if (fileTag == null)
        {
            LogTagNotAssociatedWithFile(logger, tagId, fileId);
            return;
        }

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            if (fileTag.Source == TagSource.User)
                // Manually added by the user, nothing worth remembering once it's gone
                file.FileTags!.Remove(fileTag);
            else
                // Auto/Embedded: tombstone so the next enrichment sync doesn't reapply it
                fileTag.Source = TagSource.Suppressed;

            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);

            LogTagRemovedFromFile(logger, tagId, fileId);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            LogRemoveTagFromFileFailed(logger, ex, tagId, fileId);
            throw;
        }
    }

    public async Task<PaginatedResult<FileResult>> FindFilesByTagsAsync(FileTagSearchQuery query,
        CancellationToken ct = default)
    {
        if (query.TagIds == null || !query.TagIds.Any())
            throw new ArgumentException("At least one tag ID must be provided");

        if (query.CurrentPage < 0)
            throw new ArgumentException("Page number must be non-negative");

        if (query.PageSize <= 0 || query.PageSize > 100)
            throw new ArgumentException("Page size must be between 1 and 100");

        try
        {
            return await unitOfWork.Files.FindFilesByTagsAsync(query, ct);
        }
        catch (Exception ex)
        {
            LogFindFilesByTagsFailed(logger, ex);
            throw;
        }
    }

    public async Task<ICollection<TagDto>> GetTagsForFileAsync(Guid userId, Guid fileId,
        CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.GetFileWithTagsAsync(userId, fileId, ct);

        if (file is not { DeletedAt: null })
            throw new InvalidOperationException($"File {fileId} not found");

        return file.Tags;
    }

    public async Task<PaginatedResult<TagDto>> FindTagsAsync(
        TagSearchQuery query,
        CancellationToken ct = default)
    {
        if (query.CurrentPage < 0)
            throw new ArgumentException("Page number must be non-negative");

        if (query.PageSize is <= 0 or > 100)
            throw new ArgumentException("Page size must be between 1 and 100");

        try
        {
            var (tags, totalCount) = await unitOfWork.Tags.FindTagsAsync(query, ct);

            return new PaginatedResult<TagDto>
            {
                Items = [.. tags],
                CurrentPage = query.CurrentPage,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
            };
        }
        catch (Exception ex)
        {
            LogFindTagsFailed(logger, ex);
            throw;
        }
    }
}