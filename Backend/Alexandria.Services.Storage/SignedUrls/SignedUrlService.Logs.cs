using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.SignedUrls;

public sealed partial class SignedUrlService
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating share link for file {FileId} by user {UserId} (pinned version: {FileVersionId})")]
    private partial void LogCreatingShareLink(Guid fileId, Guid userId, Guid? fileVersionId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Generating presigned download URL for file {FileId} version {FileVersionId}, token {Token}")]
    private partial void LogGeneratingDownloadUrl(Guid fileId, Guid? fileVersionId, string token);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Revoking share link {LinkId} for user {UserId}")]
    private partial void LogRevokingShareLink(Guid linkId, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Share link token {Token} not found or expired")]
    private partial void LogInvalidToken(string token);
}