using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Microsoft.Extensions.Logging;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Services.Storage;

public partial class FileService(
    IUnitOfWork unitOfWork,
    IDirectoryService dirService,
    ILogger<FileService> logger) : IFileService
{
    public async Task FolderWithOwnershipExistsAsync(Guid? directoryId, Guid ownerId, CancellationToken ct = default)
    {
        if (directoryId is null) return;

        var exists = await dirService.DirectoryExistsWithOwnershipAsync((Guid)directoryId, ownerId, ct);
        if (!exists) throw new DirectoryNotFoundException();
    }

    public async Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default)
    {
        await FolderWithOwnershipExistsAsync(destinationId, userId, ct);

        await unitOfWork.Files.MoveFilesAsync(fileIds, destinationId, userId, ct);
    }

    public async Task CopyFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        await FolderWithOwnershipExistsAsync(destinationId, userId, ct);

        await unitOfWork.Files.CopyFilesAsync(fileIds, destinationId, userId, ct);

        await unitOfWork.CommitAsync(ct);
    }

    public async Task<File?> GetFileMetadataAsync(Guid fileId, CancellationToken ct = default) =>
        await unitOfWork.Files.GetByIdAsync(fileId, ct);

    public async Task<bool> VersionBelongsToUserAsync(Guid versionId, Guid userId, CancellationToken ct = default) =>
        await unitOfWork.Files.VersionBelongsToUserAsync(versionId, userId, ct);


    public async Task<FileMetadata?>
        GetUserFileMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default) =>
        await unitOfWork.Files.GetUserFileMetadataAsync(fileId, userId, ct);

    public async Task DeleteFilesAsync(Guid[] fileIds, Guid userId, bool hardDelete = false,
        CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await unitOfWork.Files.MarkAsDeletedAsync(fileIds, userId, ct);

            if (hardDelete)
                await unitOfWork.FileVersions.DeleteFileVersionsAsync(fileIds, userId, ct);
            else
                await unitOfWork.FileVersions.SoftDeleteFileVersionsAsync(fileIds, userId, ct);

            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            throw new InvalidOperationException($"Delete failed: {ex.Message}", ex);
        }
    }

    public async Task<File> UpdateFileMetadataAsync(
        Guid fileId,
        Guid updatedBy,
        string? newName = null,
        bool? hasPreview = null,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Updating file metadata: FileId={FileId}, NewName={NewName}, HasPreview={HasPreview}, UpdatedBy={UpdatedBy}",
            fileId, newName, hasPreview, updatedBy);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var fileEntity = await unitOfWork.Files.GetByIdAsync(fileId, ct);
            if (fileEntity == null)
            {
                logger.LogWarning("File not found for metadata update: FileId={FileId}", fileId);
                throw new InvalidOperationException($"File with ID {fileId} not found.");
            }

            // Update only provided fields
            if (!string.IsNullOrEmpty(newName))
            {
                logger.LogDebug("Updating file name: FileId={FileId}, OldName={OldName}, NewName={NewName}",
                    fileId, fileEntity.Name, newName);
                fileEntity.Name = newName;
            }

            if (hasPreview.HasValue)
            {
                logger.LogDebug("Updating preview status: FileId={FileId}, HasPreview={HasPreview}",
                    fileId, hasPreview.Value);
                fileEntity.HasPreview = hasPreview.Value;
                if (hasPreview.Value) fileEntity.PreviewGeneratedAt = DateTime.UtcNow;
            }

            fileEntity.UpdatedBy = updatedBy;

            var updatedFile = await unitOfWork.Files.UpdateAsync(fileEntity, ct);
            await unitOfWork.CommitAsync(ct);

            logger.LogInformation(
                "File metadata updated successfully: FileId={FileId}",
                fileId);

            return updatedFile;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "File metadata update failed: FileId={FileId}",
                fileId);

            await unitOfWork.RollbackAsync(ct);
            throw new InvalidOperationException($"Update failed: {ex.Message}", ex);
        }
    }

    public async Task<PaginatedResult<FileResult>> GetRootFilesAsync(Guid ownerId, int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default)
    {
        return await unitOfWork.Directories.GetRootFilesAsync(ownerId, page, pageSize, sortBy,
            sortDirection,
            ct);
    }

    public async Task<PaginatedResult<FileResult>> GetFilesByDirectoryIdAsync(Guid directoryId,
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default)
    {
        var directoryExists =
            await unitOfWork.Directories.ExistsAsync(d => d.Id == directoryId && d.OwnerId == ownerId, ct);

        if (!directoryExists) throw new Common.Exceptions.Directories.DirectoryNotFoundException(directoryId);

        return await unitOfWork.Files.GetFilesByDirectoryIdAsync(directoryId,
            ownerId,
            page, pageSize,
            sortDirection,
            sortBy,
            ct
        );
    }

    public async Task<PaginatedResult<FileResult>> SearchFileAsync(FileSearchQuery query, Guid userId,
        CancellationToken ct = default)
    {
        return await unitOfWork.Files.FindFilesAsync(query, userId, ct);
    }

    public async Task<int> GetFileCountAsync(string? mimeTypeFilter = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(mimeTypeFilter)) return await unitOfWork.Files.CountAsync(null, ct);

        return await unitOfWork.Files.CountAsync(f => f.MimeType == mimeTypeFilter, ct);
    }

    public async Task<int> RestoreFilesAsync(Guid[] fileIds, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var restoredFiles = await unitOfWork.Files.RestoreFilesAsync(fileIds, userId, ct);
            await unitOfWork.FileVersions.RestoreFileVersionsAsync(fileIds, userId, ct);

            await unitOfWork.CommitAsync(ct);

            return restoredFiles;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> GetFileCountPerUserAsync(Guid userId, bool deletedOnly, CancellationToken ct = default)
    {
        return await unitOfWork.Files.GetFileCountPerUserAsync(userId, deletedOnly, ct);
    }

    public async Task<long> GetFileSizePerUserAsync(Guid userId, bool deletedOnly, CancellationToken ct = default)
    {
        return await unitOfWork.Files.GetStorageUsagePerUserAsync(userId, deletedOnly, ct);
    }

    public async Task<PaginatedResult<FileVersionDto>> GetVersionsForFileAsync(Guid fileId, Guid userId, int page = 1,
        int pageSize = 10, CancellationToken ct = default)
    {
        return await unitOfWork.FileVersions.GetVersionsForFileAsync(fileId: fileId, ownerId: userId, page: page,
            pageSize: pageSize, ct);
    }

    public async Task ChangeActiveVersionAsync(Guid versionId, Guid fileId, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.Files.ChangeActiveVersionAsync(versionId, fileId, userId, ct);
    }

    public async Task RemoveFileVersionAsync(Guid fileVersionId, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var version =
                await unitOfWork.FileVersions.FirstOrDefaultAsync(
                    f => f.Id == fileVersionId && f.File.OwnerId == userId, ct) ??
                throw new InvalidOperationException("File version not found");

            var fileId = version.FileId;
            var currentVersionId = version.File.CurrentVersionId;

            await unitOfWork.FileVersions.RemoveAsync(fileVersionId, userId, ct);

            var remainingCount = await unitOfWork.FileVersions
                .CountAsync(f => f.FileId == fileId && f.DeletedAt == null, ct);

            if (remainingCount == 0)
            {
                var file = await unitOfWork.Files.GetByIdAsync(fileId, ct) ??
                           throw new InvalidOperationException("File not found");

                unitOfWork.Files.Remove(file);
                await unitOfWork.CommitAsync(ct);
                return;
            }

            if (currentVersionId == fileVersionId)
            {
                var mostRecent = await unitOfWork.FileVersions.GetMostRecentAsync(fileId, userId, ct) ??
                                 throw new InvalidOperationException("No active version available");

                await unitOfWork.Files.UpdateCurrentVersionAsync(fileId, mostRecent.Id, ct);
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<FileResult> GetFileWithOwnershipByIdAsync(Guid fileId, Guid userId,
        CancellationToken ct = default)
    {
        return await unitOfWork.Files.GetFileWithOwnershipByIdAsync(fileId: fileId, userId: userId, ct) ??
               throw new InvalidOperationException("File with ID not found");
    }

    public async Task RestoreFileVersionAsync(Guid fileVersionId, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await unitOfWork.FileVersions.RestoreFileVersionAsync(fileVersionId, userId, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public Task<bool> IsVideo(Guid versionId, Guid userId,
        CancellationToken ct = default) =>
        unitOfWork.ContentObjects.IsVideo(versionId, userId, ct);

    public async Task<PaginatedResult<FileResult>> GetFilesForStreamingAsync(Guid userId, int page, int pageSize,
        CancellationToken ct = default)
        => await unitOfWork.Files.GetFilesForStreamingAsync(userId, page, pageSize, ct);
}