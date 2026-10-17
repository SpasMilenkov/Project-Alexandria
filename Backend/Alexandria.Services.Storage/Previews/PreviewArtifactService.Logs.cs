using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Previews;

public partial class PreviewArtifactService
{
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
}