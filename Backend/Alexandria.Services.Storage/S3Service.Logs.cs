using System.Net;
using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage;

public partial class S3Service
{
    // Temp object cleanup

    [LoggerMessage(1001, LogLevel.Debug, "Cleaned up temp object: {Bucket}/{Key}")]
    private static partial void LogTempObjectCleaned(ILogger logger, string bucket, string key);

    [LoggerMessage(1002, LogLevel.Debug, "Temp object not found (already deleted): {Bucket}/{Key}")]
    private static partial void LogTempObjectNotFound(ILogger logger, string bucket, string key);

    [LoggerMessage(1003, LogLevel.Warning, "Failed to cleanup temp object: {Bucket}/{Key}")]
    private static partial void LogTempObjectCleanupFailed(ILogger logger, Exception ex, string bucket, string key);

    // File record creation

    [LoggerMessage(1004, LogLevel.Information, "Creating new file record: Name={FileName}")]
    private static partial void LogCreatingFileRecord(ILogger logger, string fileName);

    // Preview upload

    [LoggerMessage(1005, LogLevel.Information,
        "Starting preview upload: Bucket={BucketName}, Object={ObjectName}, VersionId={VersionId}")]
    private static partial void LogStartingPreviewUpload(
        ILogger logger, string bucketName, string objectName, Guid versionId);

    [LoggerMessage(1006, LogLevel.Information,
        "Updating existing preview record: PreviewId={PreviewId}, Path={Path}")]
    private static partial void LogUpdatingExistingPreview(ILogger logger, Guid previewId, string path);

    [LoggerMessage(1007, LogLevel.Information,
        "Creating new preview record: Path={Path}, VersionId={VersionId}")]
    private static partial void LogCreatingNewPreview(ILogger logger, string path, Guid versionId);

    [LoggerMessage(1008, LogLevel.Information,
        "Preview upload completed successfully: PreviewId={PreviewId}, Size={Size}")]
    private static partial void LogPreviewUploadCompleted(ILogger logger, Guid previewId, long size);

    [LoggerMessage(1009, LogLevel.Error,
        "Preview upload failed: Bucket={BucketName}, Object={ObjectName}, VersionId={VersionId}")]
    private static partial void LogPreviewUploadFailed(
        ILogger logger, Exception ex, string bucketName, string objectName, Guid versionId);

    [LoggerMessage(1010, LogLevel.Warning,
        "Attempting cleanup: Deleting preview from storage: Bucket={BucketName}, Object={ObjectName}")]
    private static partial void LogAttemptingPreviewCleanup(ILogger logger, string bucketName, string objectName);

    [LoggerMessage(1011, LogLevel.Information,
        "Cleanup successful: Preview deleted from storage: Bucket={BucketName}, Object={ObjectName}")]
    private static partial void LogPreviewCleanupSuccessful(ILogger logger, string bucketName, string objectName);

    [LoggerMessage(1012, LogLevel.Error,
        "Failed to cleanup preview after upload failure: Bucket={BucketName}, Object={ObjectName}")]
    private static partial void LogPreviewCleanupFailed(
        ILogger logger, Exception ex, string bucketName, string objectName);

    // Media data upload

    [LoggerMessage(1013, LogLevel.Information,
        "Starting media data upload: VersionId={VersionId}, PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}")]
    private static partial void LogStartingMediaDataUpload(
        ILogger logger, Guid versionId, string previewKey, string thumbnailKey);


    [LoggerMessage(1015, LogLevel.Debug, "Uploading preview video: PreviewKey={PreviewKey}")]
    private static partial void LogUploadingPreviewVideo(ILogger logger, string previewKey);

    [LoggerMessage(1016, LogLevel.Debug, "Uploading thumbnail: ThumbnailKey={ThumbnailKey}")]
    private static partial void LogUploadingThumbnail(ILogger logger, string thumbnailKey);

    [LoggerMessage(1017, LogLevel.Information,
        "Updating existing media metadata: MetadataId={MetadataId}, FileId={FileId}")]
    private static partial void LogUpdatingMediaMetadata(ILogger logger, Guid metadataId, Guid fileId);

    [LoggerMessage(1018, LogLevel.Information, "Creating new media metadata record: FileId={FileId}")]
    private static partial void LogCreatingMediaMetadata(ILogger logger, Guid fileId);

    [LoggerMessage(1019, LogLevel.Debug, "Updating existing preview record: PreviewId={PreviewId}")]
    private static partial void LogUpdatingPreviewRecord(ILogger logger, Guid previewId);

    [LoggerMessage(1020, LogLevel.Debug, "Creating new preview record for media: VersionId={VersionId}")]
    private static partial void LogCreatingPreviewForMedia(ILogger logger, Guid versionId);

    [LoggerMessage(1021, LogLevel.Information,
        "Media data upload completed successfully: VersionId={VersionId}, PreviewSize={PreviewSize}")]
    private static partial void LogMediaDataUploadCompleted(ILogger logger, Guid versionId, long previewSize);

    [LoggerMessage(1022, LogLevel.Error,
        "Failed to upload media data: VersionId={VersionId}, PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}")]
    private static partial void LogMediaDataUploadFailed(
        ILogger logger, Exception ex, Guid versionId, string previewKey, string thumbnailKey);

    [LoggerMessage(1023, LogLevel.Warning,
        "Attempting cleanup of media data: PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}")]
    private static partial void LogAttemptingMediaDataCleanup(ILogger logger, string previewKey, string thumbnailKey);

    [LoggerMessage(1024, LogLevel.Information,
        "Media data cleanup successful: PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}")]
    private static partial void LogMediaDataCleanupSuccessful(ILogger logger, string previewKey, string thumbnailKey);

    [LoggerMessage(1025, LogLevel.Error,
        "Failed to clean up media data after upload failure: PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}")]
    private static partial void LogMediaDataCleanupFailed(
        ILogger logger, Exception ex, string previewKey, string thumbnailKey);

    // Cached preview

    [LoggerMessage(1026, LogLevel.Debug, "Retrieving cached preview: VersionId={VersionId}")]
    private static partial void LogRetrievingCachedPreview(ILogger logger, Guid versionId);

    [LoggerMessage(1027, LogLevel.Debug, "No preview available for version: VersionId={VersionId}")]
    private static partial void LogNoPreviewAvailable(ILogger logger, Guid versionId);

    [LoggerMessage(1028, LogLevel.Debug,
        "File categorized as {Category} for preview: VersionId={VersionId}, MimeType={MimeType}")]
    private static partial void LogFileCategorized(
        ILogger logger, FileCategory category, Guid versionId, string mimeType);

    [LoggerMessage(1029, LogLevel.Error,
        "Failed to retrieve cached preview: VersionId={VersionId}, Kind={Kind}")]
    private static partial void LogFailedToRetrieveCachedPreview(
        ILogger logger, Exception ex, Guid versionId, PreviewKind kind);

    // File download

    [LoggerMessage(1030, LogLevel.Warning, "File not found in database during download: Id={VersionId}")]
    private static partial void LogFileNotFoundForDownload(ILogger logger, Guid versionId);

    [LoggerMessage(1031, LogLevel.Information,
        "File download stream acquired: Bucket={BucketName}, Object={ObjectName}, Size={ContentLength}")]
    private static partial void LogFileDownloadStreamAcquired(
        ILogger logger, string bucketName, string objectName, long contentLength);

    [LoggerMessage(1032, LogLevel.Error,
        "S3 error during file download: Bucket={BucketName}, Object={ObjectName}, StatusCode={StatusCode}")]
    private static partial void LogS3ErrorDuringDownload(
        ILogger logger, Exception ex, string bucketName, string objectName, HttpStatusCode statusCode);

    [LoggerMessage(1033, LogLevel.Error, "Failed to download file: Bucket={BucketName}, Object={ObjectName}")]
    private static partial void LogFailedToDownloadFile(ILogger logger, Exception ex, string bucketName,
        string objectName);

    // File streaming

    [LoggerMessage(1034, LogLevel.Information, "Streaming file version to destination: FileId={VersionId}")]
    private static partial void LogStreamingFileToDestination(ILogger logger, Guid versionId);

    [LoggerMessage(1035, LogLevel.Warning, "File not found for streaming: FileId={VersionId}")]
    private static partial void LogFileNotFoundForStreaming(ILogger logger, Guid versionId);

    [LoggerMessage(1036, LogLevel.Debug,
        "Streaming file content: VersionId={VersionId}, Size={ContentLength}")]
    private static partial void LogStreamingFileContent(
        ILogger logger, Guid versionId, long contentLength);

    [LoggerMessage(1037, LogLevel.Information, "File streaming completed: VersionId={VersionId}")]
    private static partial void LogFileStreamingCompleted(ILogger logger, Guid versionId);

    [LoggerMessage(1038, LogLevel.Error,
        "S3 error during file streaming: VersionId={versionId}, StatusCode={StatusCode}")]
    private static partial void LogS3ErrorDuringStreaming(
        ILogger logger, Exception ex, Guid versionId, HttpStatusCode statusCode);

    [LoggerMessage(1039, LogLevel.Error, "Failed to stream file: VersionId={VersionId}")]
    private static partial void LogFailedToStreamFile(ILogger logger, Exception ex, Guid versionId);

    // Initiate upload

    [LoggerMessage(1040, LogLevel.Error,
        "File upload failed: Bucket={BucketName}, Object={ObjectName}, Error={ErrorMessage}")]
    private static partial void LogFileUploadInitiateFailed(
        ILogger logger, Exception ex, string bucketName, string objectName, string errorMessage);

    // Finalize upload

    [LoggerMessage(1041, LogLevel.Information, "Upload finalized: FileId={FileId}, Size={Size}")]
    private static partial void LogUploadFinalized(ILogger logger, Guid fileId, long size);

    [LoggerMessage(1042, LogLevel.Error, "Finalize upload failed: {Object}")]
    private static partial void LogFinalizeUploadFailed(ILogger logger, Exception ex, string @object);

    [LoggerMessage(1043, LogLevel.Debug,
        "Downloading content object {ContentObjectId} from {Bucket}/{Key}")]
    private static partial void LogDownloadingContentObject(
        ILogger logger, Guid contentObjectId, string bucket, string key);

    [LoggerMessage(1044, LogLevel.Debug,
        "Content object {ContentObjectId} downloaded to {LocalFilePath}")]
    private static partial void LogContentObjectDownloaded(
        ILogger logger, Guid contentObjectId, string localFilePath);

    [LoggerMessage(1045, LogLevel.Error,
        "S3 error downloading content object {ContentObjectId} from {Bucket}/{Key} — status {StatusCode}")]
    private static partial void LogContentObjectDownloadS3Error(
        ILogger logger, Exception ex, Guid contentObjectId, string bucket, string key, HttpStatusCode statusCode);

    [LoggerMessage(1046, LogLevel.Error,
        "Failed to download content object {ContentObjectId} from {Bucket}/{Key}")]
    private static partial void LogContentObjectDownloadFailed(
        ILogger logger, Exception ex, Guid contentObjectId, string bucket, string key);

    [LoggerMessage(1047, LogLevel.Debug,
        "Uploading {FileCount} streaming file(s) from '{LocalDirectory}' to prefix '{KeyPrefix}'")]
    private static partial void LogUploadingStreamingOutput(
        ILogger logger, string localDirectory, string keyPrefix, int fileCount);

    [LoggerMessage(1048, LogLevel.Debug,
        "PUT streaming file {Bucket}/{Key}")]
    private static partial void LogStreamingFilePut(
        ILogger logger, string bucket, string key);

    [LoggerMessage(1049, LogLevel.Error,
        "Failed to PUT streaming file {Bucket}/{Key}")]
    private static partial void LogStreamingFilePutFailed(
        ILogger logger, Exception ex, string bucket, string key);

    [LoggerMessage(1050, LogLevel.Information,
        "Streaming output upload complete: {FileCount} file(s) under prefix '{KeyPrefix}'")]
    private static partial void LogStreamingOutputUploaded(
        ILogger logger, string keyPrefix, int fileCount);

    // Preview deletion

    [LoggerMessage(1051, LogLevel.Information,
        "Preview deleted: PreviewId={PreviewId}, FreedBytes={FreedBytes}")]
    private static partial void LogPreviewDeleted(ILogger logger, Guid previewId, long freedBytes);

    [LoggerMessage(1052, LogLevel.Information,
        "Previews deleted by file: FileId={FileId}, Count={Count}, FreedBytes={FreedBytes}")]
    private static partial void LogPreviewsDeletedByFile(
        ILogger logger, Guid fileId, int count, long freedBytes);

    [LoggerMessage(1053, LogLevel.Information,
        "Preview object still referenced, keeping S3 object: PreviewId={PreviewId}, Key={Key}")]
    private static partial void LogPreviewObjectShared(ILogger logger, Guid previewId, string key);

    [LoggerMessage(1054, LogLevel.Debug,
        "Preview object already absent in storage: {Bucket}/{Key}")]
    private static partial void LogPreviewObjectNotFound(ILogger logger, string bucket, string key);

    // Storage quota (warn-only)

    [LoggerMessage(1055, LogLevel.Warning,
        "Upload would exceed storage quota: UserId={UserId}, UsedBytes={UsedBytes}, " +
        "ContentLength={ContentLength}, QuotaBytes={QuotaBytes}")]
    private static partial void LogStorageQuotaExceeded(
        ILogger logger, Guid userId, long usedBytes, long contentLength, long quotaBytes);

    [LoggerMessage(1056, LogLevel.Debug,
        "Storage quota check failed for user: UserId={UserId}")]
    private static partial void LogStorageQuotaCheckFailed(
        ILogger logger, Exception ex, Guid userId);
}