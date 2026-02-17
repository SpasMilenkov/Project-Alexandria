using Common;
using Common.Services;
using DTO.Files;
using Microsoft.Extensions.Logging;
using Models.Enumerators;
using File = Models.File;

namespace Storage;

public class FileService(
    IUnitOfWork unitOfWork,
    IDirectoryService dirService,
    ILogger<FileService> logger) : IFileService
{
    public async Task FolderWithOwnershipExists(Guid? directoryId, Guid ownerId, CancellationToken ct = default)
    {
        if (directoryId is null) return;

        var exists = await dirService.DirectoryExistsWithOwnershipAsync((Guid)directoryId, ownerId, ct);
        if (!exists) throw new DirectoryNotFoundException();
    }

    public async Task MoveFilesAsync(Guid[] fileIds, Guid? destinationId, Guid userId, CancellationToken ct = default)
    {
        await FolderWithOwnershipExists(destinationId, userId, ct);

        await unitOfWork.Files.MoveFilesAsync(fileIds, destinationId, userId, ct);
    }

    public async Task CopyFilesAsync(Guid[] fileIds, Guid destinationId, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        await FolderWithOwnershipExists(destinationId, userId, ct);

        await unitOfWork.Files.CopyFilesAsync(fileIds, destinationId, userId, ct);

        await unitOfWork.CommitAsync(ct);
    }

    public async Task<File?> GetFileMetadata(Guid fileId, CancellationToken ct = default) =>
        await unitOfWork.Files.GetByIdAsync(fileId, ct);

    public async Task<FileMetadata?>
        GetUserFileMetadataAsync(Guid fileId, Guid userId, CancellationToken ct = default) =>
        await unitOfWork.Files.GetUserFileMetadataAsync(fileId, userId, ct);

    public async Task<FileSummary?> GetFileSummary(Guid fieldId, CancellationToken ct = default) =>
        await unitOfWork.Files.GetFileNameAndMimeType(fieldId, ct);

    public async Task<IEnumerable<File>> GetFilesByMimeType(string mimeType, CancellationToken ct = default) =>
        await unitOfWork.Files.FindAsync(f => f.MimeType == mimeType, ct);

    public async Task DeleteFiles(Guid[] fileIds, Guid userId, bool hardDelete = false, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await unitOfWork.Files.MarkAsDeleted(fileIds, userId, ct);

            if (hardDelete)
                await unitOfWork.FileVersions.DeleteFileVersions(fileIds, userId, ct);
            else
                await unitOfWork.FileVersions.SoftDeleteFileVersions(fileIds, userId, ct);

            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync(ct);
            throw new InvalidOperationException($"Delete failed: {ex.Message}", ex);
        }
    }

    public async Task<File> UpdateFileMetadata(
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

    public async Task<PaginatedResult<FileResult>> GetFilesByDirectoryId(Guid directoryId,
        Guid ownerId,
        int page = 1,
        int pageSize = 25,
        SortBy sortBy = SortBy.Name,
        SortDirection sortDirection = SortDirection.Asc,
        CancellationToken ct = default)
    {
        var directoryExists =
            await unitOfWork.Directories.ExistsAsync(d => d.Id == directoryId && d.OwnerId == ownerId);

        if (!directoryExists) throw new Directories.Exceptions.DirectoryNotFoundException(directoryId);

        return await unitOfWork.Files.GetFilesByDirectoryIdAsync(directoryId,
            ownerId,
            page, pageSize,
            sortDirection,
            sortBy,
            ct
        );
    }

    public async Task<PaginatedResult<FileResult>> SearchFile(FileSearchQuery query, Guid userId,
        CancellationToken ct = default)
    {
        return await unitOfWork.Files.FindFiles(query, userId, ct);
    }

    public async Task<int> GetFileCount(string? mimeTypeFilter = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(mimeTypeFilter)) return await unitOfWork.Files.CountAsync(null, ct);

        return await unitOfWork.Files.CountAsync(f => f.MimeType == mimeTypeFilter, ct);
    }

    public async Task<int> RestoreFiles(Guid[] fileIds, Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var restoredFiles = await unitOfWork.Files.RestoreFiles(fileIds, userId, ct);
            await unitOfWork.FileVersions.RestoreFileVersions(fileIds, userId, ct);

            await unitOfWork.CommitAsync(ct);

            return restoredFiles;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}
