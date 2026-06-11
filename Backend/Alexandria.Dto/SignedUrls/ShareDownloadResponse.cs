namespace Alexandria.Dto.SignedUrls;

public sealed class ShareDownloadResponse
{
    /// <summary>Presigned S3 URL the client should redirect to for the actual download.</summary>
    public required string PresignedUrl { get; init; }

    public required string FileName { get; init; }
    public required string MimeType { get; init; }
}