using DTO.Files;
using DTO.Tags;
using Models;

namespace Common.Services;

/// <summary>
/// Service for managing file tags and their associations with files.
/// All methods support pagination and complex querying.
/// </summary>
public interface IFileTagService
{
    /// <summary>
    /// Creates a new tag for a specific user.
    /// </summary>
    /// <param name="name">The name of the tag (will be trimmed)</param>
    /// <param name="description">Optional description of the tag</param>
    /// <param name="userId">The ID of the user creating the tag</param>
    /// <param name="ct">Cancellation token</param>
    /// <param name="color">Color of the tag selected by the user</param>
    /// <param name="icon">Icon of of the tag selected by the user</param>
    /// <returns>The created tag</returns>
    /// <exception cref="ArgumentException">If name is empty or userId is invalid</exception>
    /// <exception cref="InvalidOperationException">If tag already exists for user</exception>
    Task<Tag> CreateAsync(string name, string color, string icon, string? description, Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing tag's name.
    /// </summary>
    /// <param name="tagId">The ID of the tag to update</param>
    /// <param name="name">The new name for the tag</param>
    /// <param name="userId">The ID of the user updating the tag (must be owner)</param>
    /// <param name="description">The tag description</param>
    /// <param name="ct">Cancellation token</param>
    /// <param name="color">The color of the tag</param>
    /// <param name="icon">The tag icon</param>
    /// <returns>The updated tag</returns>
    /// <exception cref="InvalidOperationException">If tag not found</exception>
    /// <exception cref="UnauthorizedAccessException">If user doesn't own the tag</exception>
    Task<Tag> UpdateTagAsync(Guid tagId,
        Guid userId,
        string? name,
        string? color,
        string? icon,
        string? description, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a tag (sets DeletedAt timestamp).
    /// </summary>
    /// <param name="tagId">The ID of the tag to delete</param>
    /// <param name="userId">The ID of the user deleting the tag (must be owner)</param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="InvalidOperationException">If tag not found</exception>
    /// <exception cref="UnauthorizedAccessException">If user doesn't own the tag</exception>
    Task DeleteTagAsync(Guid tagId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Adds multiple tags to a file. Only adds tags owned by the specified user.
    /// </summary>
    /// <param name="fileId">The ID of the file</param>
    /// <param name="tagIds">Collection of tag IDs to add</param>
    /// <param name="userId">The ID of the user performing the operation</param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="InvalidOperationException">If file not found</exception>
    Task AddTagsToFileAsync(Guid fileId, ICollection<Guid> tagIds, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Removes a tag from a file.
    /// </summary>
    /// <param name="fileId">The ID of the file</param>
    /// <param name="tagId">The ID of the tag to remove</param>
    /// <param name="userId">The ID of the user performing the operation (must own the tag)</param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="InvalidOperationException">If file not found</exception>
    /// <exception cref="UnauthorizedAccessException">If user doesn't own the tag</exception>
    Task RemoveTagFromFileAsync(Guid fileId, Guid tagId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Finds files based on tag criteria with advanced filtering and pagination.
    /// Supports multiple match types: Any (OR), All (AND), or Exact match.
    /// </summary>
    /// <param name="query">Search query with filters and pagination parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated result of files matching the criteria</returns>
    Task<PaginatedResult<FileResult>> FindFilesByTagsAsync(FileTagSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// Gets all tags associated with a specific file.
    /// </summary>
    /// <param name="fileId">The ID of the file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of tags for the file</returns>
    /// <exception cref="InvalidOperationException">If file not found</exception>
    Task<ICollection<TagDto>> GetTagsForFileAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Finds tags based on various criteria with pagination support.
    /// Can filter by user, creation/update dates, name search, and more.
    /// </summary>
    /// <param name="query">Search query with filters and pagination parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated result of tags matching the criteria</returns>
    Task<PaginatedResult<TagDto>> FindTagsAsync(TagSearchQuery query, CancellationToken ct = default);
}