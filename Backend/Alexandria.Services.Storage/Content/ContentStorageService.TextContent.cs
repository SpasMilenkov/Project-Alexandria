using System.Net;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Notes;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage.Content.Mime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Alexandria.Services.Storage.Content;

// Text written by the editor goes through the same commit as an upload, so deduplication,
// promotion and version numbering stay identical. Only the staging differs: the server writes
// the bytes itself, straight to the sealed prefix no signed URL can reach.
public partial class ContentStorageService
{
    public async Task<VersionText> ReadVersionTextAsync(Guid versionId, Guid userId, CancellationToken ct = default)
    {
        var version = await unitOfWork.FileVersions.FirstOrDefaultAsync(
                          v => v.Id == versionId && v.File.OwnerId == userId && v.DeletedAt == null, ct)
                      ?? throw new FileVersionNotFoundException(versionId);

        EnsureReadableText(version);

        var content = await ReadContentObjectBytesAsync(version.ContentObjectId, ct);

        return new VersionText(version.FileId, version.Id, version.MimeType, content);
    }

    public async Task<FileContentSaveResult> SaveTextContentAsync(
        Guid fileId,
        Guid parentVersionId,
        Guid? draftRevision,
        string text,
        Guid userId,
        CancellationToken ct = default)
    {
        var file = await unitOfWork.Files.FirstOrDefaultAsync(
                       f => f.Id == fileId && f.OwnerId == userId && f.DeletedAt == null, ct)
                   ?? throw new FileRecordNotFoundException(fileId);

        var parent = await unitOfWork.FileVersions.FirstOrDefaultAsync(
                         v => v.Id == parentVersionId && v.FileId == fileId && v.DeletedAt == null, ct)
                     ?? throw new FileVersionNotFoundException(parentVersionId);

        EditableText.EnsureEditable(parent);

        if (file.CurrentVersionId != parentVersionId)
            throw new FileVersionConflictException(fileId, parentVersionId, file.CurrentVersionId);

        // The check under the row lock is the one that counts. This one spares staging an upload
        // that a draft written elsewhere would refuse anyway, which is the common way to lose.
        var storedRevision = await unitOfWork.FileDrafts.GetRevisionAsync(fileId, ct);

        if (storedRevision is { } stored && stored != draftRevision)
            throw new FileDraftConflictException(fileId, draftRevision, stored);

        var content = TextContentBytes.Encode(fileId, text);
        var hash = TextContentBytes.Hash(content);
        var target = new VersionTarget(fileId, parentVersionId, parent.MimeType, draftRevision);

        // Saving what is already stored would only add a duplicate version to the history, but a
        // draft that wandered off and came back still has to go.
        if (hash.AsSpan().SequenceEqual(parent.ContentHash))
        {
            await SettleDraftWithoutVersionAsync(target, userId, ct);

            return ToSaveResult(file, parent.Id, parent.VersionNumber, parent.Size, parent.MimeType,
                created: false, unchanged: true);
        }

        var verified = await StageTextContentAsync(content, hash, parent.MimeType, userId, ct);

        UploadResult committed;

        try
        {
            committed = await CommitVersionAsync(
                verified,
                StagedItem(verified.UploadId, file.Name, file.DirectoryId),
                target,
                requireNewFile: false,
                ct);
        }
        catch (Exception ex)
        {
            // Losing a race to another save or draft is a normal outcome the caller reports as a
            // conflict, so it isn't logged as a failure.
            if (ex is FileVersionConflictException or FileDraftConflictException)
                LogTextSaveLostRace(logger, fileId, parentVersionId);
            else
                LogTextSaveFailed(logger, ex, fileId, parentVersionId);

            await AbandonUploadAsync(verified.UploadId, verified.TempObjectKey, CancellationToken.None);
            throw;
        }

        // The commit checked the parent is still current, so the new version comes right after it.
        return ToSaveResult(file, committed.VersionId, parent.VersionNumber + 1, committed.Size,
            committed.MimeType, created: false, unchanged: false);
    }

    public async Task<FileContentSaveResult> CreateTextFileAsync(
        string fileName,
        Guid? directoryId,
        string text,
        Guid userId,
        CancellationToken ct = default)
    {
        await fileService.FolderWithOwnershipExistsAsync(directoryId, userId, ct);

        var content = TextContentBytes.Encode(Guid.Empty, text);
        var hash = TextContentBytes.Hash(content);
        var declaredMimeType = mimeResolver.Resolve(fileName, HeaderOf(content)).MimeType;

        var verified = await StageTextContentAsync(content, hash, declaredMimeType, userId, ct);

        UploadResult committed;

        try
        {
            committed = await CommitVersionAsync(
                verified,
                StagedItem(verified.UploadId, fileName, directoryId),
                target: null,
                requireNewFile: true,
                ct);
        }
        catch (Exception ex)
        {
            LogTextCreateFailed(logger, ex, fileName, directoryId);
            await AbandonUploadAsync(verified.UploadId, verified.TempObjectKey, CancellationToken.None);
            throw;
        }

        return new FileContentSaveResult
        {
            FileId = committed.FileId,
            VersionId = committed.VersionId,
            VersionNumber = 1,
            Size = committed.Size,
            FileName = fileName,
            MimeType = committed.MimeType,
            DirectoryId = directoryId,
            Created = true,
            Unchanged = false,
        };
    }

    private static void EnsureReadableText(FileVersion version)
    {
        if (version.IsEncrypted)
            throw new FileContentNotEditableException(version.Id, "Encrypted versions can't be read as text.");

        if (version.Size > TextContentLimits.MaxBytes)
            throw new FileContentNotEditableException(version.Id, "This version is too large to open as text.");
    }

    // Called under the file's row lock, so a draft write can't slip in between the check and the
    // removal. No draft is fine: there is nothing another editor could lose.
    private async Task RemoveSettledDraftAsync(VersionTarget target, CancellationToken ct)
    {
        var draft = await unitOfWork.FileDrafts.GetByIdAsync(target.FileId, ct);

        if (draft is null) return;

        if (draft.Revision != target.DraftRevision)
            throw new FileDraftConflictException(target.FileId, target.DraftRevision, draft.Revision);

        unitOfWork.FileDrafts.Remove(draft);
    }

    private async Task SettleDraftWithoutVersionAsync(VersionTarget target, Guid userId, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            await LockTargetFileAsync(target, userId, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static byte[] HeaderOf(byte[] content) =>
        content.AsSpan(0, Math.Min(content.Length, MimeSniffing.HeaderBytes)).ToArray();

    private static UploadFinalizeItem StagedItem(Guid uploadId, string fileName, Guid? directoryId) => new()
    {
        UploadId = uploadId,
        DirectoryId = directoryId,
        FileName = fileName,
        IsEncrypted = false,
        EncryptionIv = null,
        EncryptionSalt = null,
        IntegrityTag = null,
        EncryptionHint = null,
        IterationCount = null,
    };

    private static FileContentSaveResult ToSaveResult(
        Data.Models.File file,
        Guid versionId,
        int versionNumber,
        long size,
        string mimeType,
        bool created,
        bool unchanged) => new()
    {
        FileId = file.Id,
        VersionId = versionId,
        VersionNumber = versionNumber,
        Size = size,
        FileName = file.Name,
        MimeType = mimeType,
        DirectoryId = file.DirectoryId,
        Created = created,
        Unchanged = unchanged,
    };

    // The row is created already claimed for finalize, because there is no client upload to wait
    // for. A failed write leaves it to the abandon path like any interrupted upload.
    private async Task<VerifiedUpload> StageTextContentAsync(
        byte[] content,
        byte[] hash,
        string mimeType,
        Guid userId,
        CancellationToken ct)
    {
        var tmp = config.Value.TempBucket
                  ?? throw new InvalidOperationException("Temp bucket not configured");

        var upload = new Upload
        {
            Id = Guid.NewGuid(),
            Status = UploadStatus.Finalizing,
            Hash = hash,
            Size = content.Length,
            MimeType = mimeType,
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            TempObjectKey = Guid.NewGuid().ToString(),
        };

        await unitOfWork.Uploads.AddAsync(upload, ct);
        await unitOfWork.SaveChangesAsync(ct);

        try
        {
            using var stream = new MemoryStream(content, writable: false);

            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = tmp,
                Key = $"{TempObjectKeys.Sealed}{upload.TempObjectKey}",
                InputStream = stream,
                ContentType = mimeType,
                AutoCloseStream = false,
                DisableDefaultChecksumValidation = true,
            }, ct);
        }
        catch (Exception ex)
        {
            LogTextStageFailed(logger, ex, upload.Id);
            await AbandonUploadAsync(upload.Id, upload.TempObjectKey, CancellationToken.None);
            throw;
        }

        return new VerifiedUpload
        {
            UploadId = upload.Id,
            UploadedBy = userId,
            TempObjectKey = upload.TempObjectKey,
            Hash = hash,
            Size = content.Length,
            Header = HeaderOf(content),
        };
    }

    // A version saved moments ago is still in the temp bucket until promotion copies it over, and
    // the temp copy disappears once it has. Reading the temp copy first and falling back to the
    // permanent key covers both sides of that hand-over.
    private async Task<byte[]> ReadContentObjectBytesAsync(Guid contentObjectId, CancellationToken ct)
    {
        var contentObject = await unitOfWork.ContentObjects.GetByIdAsync(contentObjectId, ct)
                            ?? throw new ContentObjectNotFoundException(contentObjectId);

        if (!contentObject.IsPromoted && contentObject.UploadId is { } uploadId && config.Value.TempBucket is { } tmp)
        {
            var upload = await unitOfWork.Uploads.GetByIdAsync(uploadId, ct);

            if (upload is not null)
            {
                var staged = await TryReadObjectAsync(tmp, $"{TempObjectKeys.Sealed}{upload.TempObjectKey}", ct);

                if (staged is not null) return staged;
            }
        }

        return await TryReadObjectAsync(config.Value.UploadBucket, contentObject.StorageKey, ct)
               ?? throw new ContentObjectNotFoundException(contentObjectId);
    }

    private async Task<byte[]?> TryReadObjectAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            using var response = await s3.GetObjectAsync(bucket, key, ct);
            using var buffer = new MemoryStream();

            await response.ResponseStream.CopyToAsync(buffer, ct);

            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            LogTextObjectMissing(logger, bucket, key);
            return null;
        }
    }
}
