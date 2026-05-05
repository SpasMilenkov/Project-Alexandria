using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Common.Services;

/// <summary>
/// Provides directory management operations for the Alexandria storage system,
/// including creation, retrieval, traversal, mutation, and soft-deletion of directories.
/// </summary>
public interface IDirectoryService
{
    /// <summary>
    /// Creates a new directory under the specified parent, or at the root level if no parent is given.
    /// </summary>
    /// <param name="name">
    /// The display name for the new directory. Must be non-empty, at most 255 characters,
    /// and must not contain any of the characters <c>/ \ : * ? " &lt; &gt; |</c>.
    /// </param>
    /// <param name="ownerId">The ID of the user who will own the directory.</param>
    /// <param name="parentId">
    /// The ID of the parent directory. Pass <c>null</c> to create the directory at the root level.
    /// </param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>A summary DTO representing the newly created directory.</returns>
    /// <exception cref="Exceptions.Directories.InvalidDirectoryNameException">
    /// Thrown when <paramref name="name"/> is empty, exceeds 255 characters, or contains forbidden characters.
    /// </exception>
    /// <exception cref="Exceptions.Directories.DirectoryAlreadyExistsException">
    /// Thrown when a directory with the same <paramref name="name"/> already exists
    /// under the same parent for the given owner.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when <paramref name="parentId"/> is specified but no directory with that ID exists.
    /// </exception>
    /// <exception cref="Exceptions.Directories.UnauthorizedDirectoryAccessException">
    /// Thrown when <paramref name="parentId"/> is specified but the parent directory
    /// is not owned by <paramref name="ownerId"/>.
    /// </exception>
    Task<DirectorySummaryDto> CreateDirectoryAsync(string name, Guid ownerId, Guid? parentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a directory subtree from a flat list of file paths, reconstructing the implied
    /// folder hierarchy and returning a mapping from each path to the ID of its immediate parent directory.
    /// </summary>
    /// <param name="paths">
    /// A list of relative file paths (e.g. <c>images/2024/photo.jpg</c>).
    /// The directory segments of each path are used to build the subtree.
    /// </param>
    /// <param name="parentId">
    /// The ID of the directory under which the subtree will be rooted.
    /// Pass <c>null</c> to root the subtree at the user's root level.
    /// </param>
    /// <param name="userId">The ID of the user who will own all created directories.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>
    /// A dictionary mapping each input path to the <see cref="Guid"/> of the directory
    /// that should contain it, or <c>null</c> if the file belongs at the root.
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when <paramref name="parentId"/> is specified but no directory with that ID exists.
    /// </exception>
    Task<Dictionary<string, Guid?>> CreateDirectorySubTreeAsync(List<string> paths, Guid? parentId, Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the raw <see cref="Directory"/> entity by ID, verifying that the requesting
    /// user is the owner.
    /// </summary>
    /// <param name="id">The ID of the directory to retrieve.</param>
    /// <param name="userId">The ID of the user requesting access.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>The <see cref="Directory"/> entity.</returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when no non-deleted directory with <paramref name="id"/> exists.
    /// </exception>
    /// <exception cref="Exceptions.Directories.UnauthorizedDirectoryAccessException">
    /// Thrown when the directory exists but is not owned by <paramref name="userId"/>.
    /// </exception>
    Task<Directory> GetDirectoryByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves directory metadata as a DTO, including owner information,
    /// verifying that the requesting user is the owner.
    /// </summary>
    /// <param name="id">The ID of the directory to retrieve.</param>
    /// <param name="userId">The ID of the user requesting access.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>A <see cref="DirectorySummaryDto"/> representing the directory.</returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when no non-deleted directory with <paramref name="id"/> exists
    /// that is owned by <paramref name="userId"/>.
    /// </exception>
    Task<DirectorySummaryDto> GetDirectoryDtoByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Searches directories owned by the specified user using the provided query parameters,
    /// including optional full-text and trigram similarity search on directory names.
    /// </summary>
    /// <param name="userId">The ID of the user whose directories are being searched.</param>
    /// <param name="query">The search and filter criteria, including optional name search and pagination.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>A paginated result set of matching <see cref="DirectorySummaryDto"/> entries.</returns>
    Task<PaginatedResult<DirectorySummaryDto>> FindDirectoryAsync(Guid userId, DirectorySearchQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated, sorted list of immediate subdirectories within the specified directory.
    /// </summary>
    /// <param name="id">The ID of the parent directory whose children are being listed.</param>
    /// <param name="userId">The ID of the user requesting the listing.</param>
    /// <param name="currentPage">The 1-based page number to retrieve. Defaults to <c>1</c>.</param>
    /// <param name="pageSize">The number of items per page. Defaults to <c>25</c>.</param>
    /// <param name="sortDirection">The sort direction. Defaults to <see cref="SortDirection.Asc"/>.</param>
    /// <param name="sortBy">The field to sort by. Defaults to <see cref="SortBy.Name"/>.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>A paginated result set of <see cref="DirectorySummaryDto"/> entries.</returns>
    Task<PaginatedResult<DirectorySummaryDto>> GetPaginatedDirectoriesAsync(Guid id, Guid userId,
        int currentPage = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated, sorted list of root-level directories (no parent) owned by the specified user.
    /// </summary>
    /// <param name="ownerId">The ID of the user whose root directories are being listed.</param>
    /// <param name="page">The 1-based page number to retrieve. Defaults to <c>1</c>.</param>
    /// <param name="pageSize">The number of items per page. Defaults to <c>25</c>.</param>
    /// <param name="sortDirection">The sort direction. Defaults to <see cref="SortDirection.Asc"/>.</param>
    /// <param name="sortBy">The field to sort by. Defaults to <see cref="SortBy.Name"/>.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>A paginated result set of root-level <see cref="DirectorySummaryDto"/> entries.</returns>
    Task<PaginatedResult<DirectorySummaryDto>> GetRootDirectoriesAsync(Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortDirection sortDirection = SortDirection.Asc,
        SortBy sortBy = SortBy.Name,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full ancestor path of a directory as an ordered list of path parts,
    /// from the root ancestor down to the specified directory.
    /// </summary>
    /// <param name="directoryId">The ID of the directory whose path is being resolved.</param>
    /// <param name="userId">The ID of the user requesting the path. Used for access verification.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>
    /// An ordered list of <see cref="PathPartDto"/> values representing each ancestor
    /// segment, starting from the root and ending at the requested directory.
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when no non-deleted directory with <paramref name="directoryId"/> exists.
    /// </exception>
    /// <exception cref="Exceptions.Directories.UnauthorizedDirectoryAccessException">
    /// Thrown when the directory is not owned by <paramref name="userId"/>.
    /// </exception>
    Task<List<PathPartDto>> GetDirectoryPathAsync(Guid directoryId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Renames a directory, verifying ownership and name uniqueness within the same parent.
    /// </summary>
    /// <param name="id">The ID of the directory to rename.</param>
    /// <param name="name">
    /// The new name. Subject to the same validation rules as <see cref="CreateDirectoryAsync"/>.
    /// </param>
    /// <param name="userId">The ID of the user performing the rename.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>A <see cref="DirectoryDto"/> representing the updated directory.</returns>
    /// <exception cref="Exceptions.Directories.InvalidDirectoryNameException">
    /// Thrown when <paramref name="name"/> fails validation.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when no non-deleted directory with <paramref name="id"/> exists.
    /// </exception>
    /// <exception cref="Exceptions.Directories.UnauthorizedDirectoryAccessException">
    /// Thrown when the directory is not owned by <paramref name="userId"/>.
    /// </exception>
    /// <exception cref="Exceptions.Directories.DirectoryAlreadyExistsException">
    /// Thrown when a sibling directory with the same <paramref name="name"/> already exists
    /// under the same parent.
    /// </exception>
    Task<DirectoryDto> UpdateDirectoryAsync(Guid id, string name, Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Moves one or more directories to a new parent, or to the root level if
    /// <paramref name="newParentId"/> is <c>null</c>. The operation is rejected if it would
    /// create a circular reference in the directory tree.
    /// </summary>
    /// <param name="ids">The IDs of the directories to move.</param>
    /// <param name="newParentId">
    /// The ID of the destination directory, or <c>null</c> to move to the root level.
    /// </param>
    /// <param name="userId">The ID of the user performing the move.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <exception cref="Exceptions.Directories.InvalidDirectoryDestinationException">
    /// Thrown when <paramref name="newParentId"/> is specified but does not exist
    /// or is not owned by <paramref name="userId"/>.
    /// </exception>
    /// <exception cref="Exceptions.Directories.DirectoryMoveException">
    /// Thrown when the move would create a circular reference, or when none of
    /// the specified directories could be moved.
    /// </exception>
    Task MoveDirectoryAsync(Guid[] ids, Guid? newParentId, Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a directory. When <paramref name="force"/> is <c>false</c>, the directory
    /// must be empty. When <paramref name="force"/> is <c>true</c>, all descendant directories
    /// and files are recursively soft-deleted as well.
    /// </summary>
    /// <param name="id">The ID of the directory to delete.</param>
    /// <param name="userId">The ID of the user performing the deletion.</param>
    /// <param name="force">
    /// When <c>true</c>, recursively deletes all contents.
    /// When <c>false</c> (default), throws if the directory is not empty.
    /// </param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when no non-deleted directory with <paramref name="id"/> exists.
    /// </exception>
    /// <exception cref="Exceptions.Directories.UnauthorizedDirectoryAccessException">
    /// Thrown when the directory is not owned by <paramref name="userId"/>.
    /// </exception>
    /// <exception cref="Exceptions.Directories.DirectoryNotEmptyException">
    /// Thrown when <paramref name="force"/> is <c>false</c> and the directory contains
    /// one or more files or subdirectories.
    /// </exception>
    Task DeleteDirectoryAsync(Guid id, Guid userId, bool force = false,
        CancellationToken ct = default);

    /// <summary>
    /// Returns whether a non-deleted directory with the specified ID exists.
    /// </summary>
    /// <param name="id">The directory ID to check.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns><c>true</c> if the directory exists and has not been soft-deleted; otherwise <c>false</c>.</returns>
    Task<bool> DirectoryExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns whether a non-deleted directory with the specified ID exists and is owned by the given user.
    /// </summary>
    /// <param name="id">The directory ID to check.</param>
    /// <param name="ownerId">The expected owner ID.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>
    /// <c>true</c> if the directory exists, has not been soft-deleted, and is owned by <paramref name="ownerId"/>;
    /// otherwise <c>false</c>.
    /// </returns>
    Task<bool> DirectoryExistsWithOwnershipAsync(Guid id, Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Returns whether the specified user has access to the given directory.
    /// Currently, access is determined solely by ownership.
    /// </summary>
    /// <param name="directoryId">The ID of the directory to check.</param>
    /// <param name="userId">The ID of the user to check access for.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns><c>true</c> if the directory exists and is owned by <paramref name="userId"/>; otherwise <c>false</c>.</returns>
    Task<bool> HasAccessToDirectoryAsync(Guid directoryId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a full recursive copy of a directory tree, including all descendant directories
    /// and files. File content objects are shared (not duplicated in storage); only the metadata
    /// records are copied. Name collisions at the destination are resolved automatically.
    /// </summary>
    /// <param name="directoryId">The ID of the source directory to copy.</param>
    /// <param name="destinationId">
    /// The ID of the destination directory, or <c>null</c> to copy to the root level.
    /// </param>
    /// <param name="userId">The ID of the user performing the copy.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when no non-deleted directory with <paramref name="directoryId"/> exists
    /// that is owned by <paramref name="userId"/>.
    /// </exception>
    Task CopyDirectoryAsync(Guid directoryId, Guid? destinationId, Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Restores one or more soft-deleted directories, along with all their descendant directories
    /// and files, provided they were deleted within the 30-day retention window.
    /// </summary>
    /// <param name="directoryIds">The IDs of the root directories to restore.</param>
    /// <param name="userId">The ID of the user performing the restore.</param>
    /// <param name="ct">Token for cooperative cancellation.</param>
    /// <returns>
    /// The total number of records restored (directories + files + file versions combined).
    /// </returns>
    /// <exception cref="Exceptions.Directories.DirectoryRestoreException">
    /// Thrown when none of the specified directories could be restored, either because they
    /// do not exist, are not owned by <paramref name="userId"/>, or have exceeded the
    /// 30-day retention window.
    /// </exception>
    Task<int> RestoreDirectories(Guid[] directoryIds, Guid userId, CancellationToken ct = default);
}