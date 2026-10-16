using System.Net;
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