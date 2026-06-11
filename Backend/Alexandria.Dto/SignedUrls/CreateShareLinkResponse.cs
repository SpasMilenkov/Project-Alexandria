using Alexandria.Data.Models;

namespace Alexandria.Dto.SignedUrls;

public sealed class CreateShareLinkResponse
{
    public required Guid Id { get; init; }
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid? FileVersionId { get; init; }

    public static CreateShareLinkResponse FromEntity(SignedUrl entity) => new()
    {
        Id = entity.Id,
        Token = entity.Token,
        ExpiresAt = entity.ExpiresAt,
        CreatedAt = entity.CreatedAt,
        FileVersionId = entity.FileVersionId,
    };
}