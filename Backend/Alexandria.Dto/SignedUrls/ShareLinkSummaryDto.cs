using Alexandria.Data.Models;

namespace Alexandria.Dto.SignedUrls;

public sealed class ShareLinkSummaryDto
{
    public required Guid Id { get; init; }
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsExpired { get; init; }
    public required bool IsRevoked { get; init; }
    public required DateTime? RevokedAt { get; init; }
    public required int AccessCount { get; init; }
    public required int? MaxAccessCount { get; init; }
    public required Guid? FileVersionId { get; init; }

    public static ShareLinkSummaryDto FromEntity(SignedUrl entity) => new()
    {
        Id = entity.Id,
        Token = entity.Token,
        ExpiresAt = entity.ExpiresAt,
        CreatedAt = entity.CreatedAt,
        IsExpired = entity.ExpiresAt < DateTime.UtcNow,
        IsRevoked = entity.DeletedAt != null,
        RevokedAt = entity.DeletedAt,
        AccessCount = entity.AccessCount,
        MaxAccessCount = entity.MaxAccessCount,
        FileVersionId = entity.FileVersionId,
    };
}