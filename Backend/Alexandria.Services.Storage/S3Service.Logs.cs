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
        "Starting preview upload: Bucket={BucketName}, Object={ObjectName}, OriginalFileId={OriginalFileId}")]
    private static partial void LogStartingPreviewUpload(
        ILogger logger, string bucketName, string objectName, Guid originalFileId);

    [LoggerMessage(1006, LogLevel.Information,
        "Updating existing preview record: PreviewId={PreviewId}, Path={Path}")]
    private static partial void LogUpdatingExistingPreview(ILogger logger, Guid previewId, string path);

    [LoggerMessage(1007, LogLevel.Information,
        "Creating new preview record: Path={Path}, OriginalFileId={OriginalFileId}")]
    private static partial void LogCreatingNewPreview(ILogger logger, string path, Guid originalFileId);

    [LoggerMessage(1008, LogLevel.Information,
        "Preview upload completed successfully: PreviewId={PreviewId}, Size={Size}")]
    private static partial void LogPreviewUploadCompleted(ILogger logger, Guid previewId, long size);

    [LoggerMessage(1009, LogLevel.Error,
        "Preview upload failed: Bucket={BucketName}, Object={ObjectName}, OriginalFileId={OriginalFileId}")]
    private static partial void LogPreviewUploadFailed(
        ILogger logger, Exception ex, string bucketName, string objectName, Guid originalFileId);

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
        "Starting media data upload: FileId={FileId}, PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}")]
    private static partial void LogStartingMediaDataUpload(
        ILogger logger, Guid fileId, string previewKey, string thumbnailKey);

    [LoggerMessage(1014, LogLevel.Error, "Original file not found for media data upload: FileId={FileId}")]
    private static partial void LogOriginalFileNotFoundForMedia(ILogger logger, Guid fileId);

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

    [LoggerMessage(1020, LogLevel.Debug, "Creating new preview record for media: FileId={FileId}")]
    private static partial void LogCreatingPreviewForMedia(ILogger logger, Guid fileId);

    [LoggerMessage(1021, LogLevel.Information,
        "Media data upload completed successfully: FileId={FileId}, PreviewSize={PreviewSize}")]
    private static partial void LogMediaDataUploadCompleted(ILogger logger, Guid fileId, long previewSize);

    [LoggerMessage(1022, LogLevel.Error,
        "Failed to upload media data: FileId={FileId}, PreviewKey={PreviewKey}, ThumbnailKey={ThumbnailKey}")]
    private static partial void LogMediaDataUploadFailed(
        ILogger logger, Exception ex, Guid fileId, string previewKey, string thumbnailKey);

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

    [LoggerMessage(1026, LogLevel.Debug, "Retrieving cached preview: FileId={FileId}")]
    private static partial void LogRetrievingCachedPreview(ILogger logger, Guid fileId);

    [LoggerMessage(1027, LogLevel.Debug, "No preview available for file: FileId={FileId}")]
    private static partial void LogNoPreviewAvailable(ILogger logger, Guid fileId);

    [LoggerMessage(1028, LogLevel.Debug,
        "File categorized as {Category} for preview: FileId={FileId}, MimeType={MimeType}")]
    private static partial void LogFileCategorized(
        ILogger logger, FileCategory category, Guid fileId, string mimeType);

    [LoggerMessage(1029, LogLevel.Error,
        "Failed to retrieve cached preview: FileId={FileId}, Category={Category}")]
    private static partial void LogFailedToRetrieveCachedPreview(
        ILogger logger, Exception ex, Guid fileId, FileCategory category);

    // File download

    [LoggerMessage(1030, LogLevel.Warning, "File not found in database during download: Id={FileId}")]
    private static partial void LogFileNotFoundForDownload(ILogger logger, Guid fileId);

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

    [LoggerMessage(1034, LogLevel.Information, "Streaming file to destination: FileId={FileId}")]
    private static partial void LogStreamingFileToDestination(ILogger logger, string fileId);

    [LoggerMessage(1035, LogLevel.Warning, "File not found for streaming: FileId={FileId}")]
    private static partial void LogFileNotFoundForStreaming(ILogger logger, string fileId);

    [LoggerMessage(1036, LogLevel.Debug,
        "Streaming file content: FileId={FileId}, Name={FileName}, Size={ContentLength}")]
    private static partial void LogStreamingFileContent(
        ILogger logger, string fileId, string fileName, long contentLength);

    [LoggerMessage(1037, LogLevel.Information, "File streaming completed: FileId={FileId}, Name={FileName}")]
    private static partial void LogFileStreamingCompleted(ILogger logger, string fileId, string fileName);

    [LoggerMessage(1038, LogLevel.Error,
        "S3 error during file streaming: FileId={FileId}, Name={FileName}, StatusCode={StatusCode}")]
    private static partial void LogS3ErrorDuringStreaming(
        ILogger logger, Exception ex, string fileId, string fileName, HttpStatusCode statusCode);

    [LoggerMessage(1039, LogLevel.Error, "Failed to stream file: FileId={FileId}, Name={FileName}")]
    private static partial void LogFailedToStreamFile(ILogger logger, Exception ex, string fileId, string fileName);

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
}