using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Dto.Extensions;

public static class StreamHistoryExtensions
{
    public static StreamHistoryResponse ToResponse(this StreamHistory history)
        => new()
        {
            Id = history.Id,
            UserId = history.UserId,
            FileId = history.FileId,
            PositionSeconds = history.PositionSeconds,
            Completed = history.Completed,
            LastAccessedAt = history.LastAccessedAt,
            CreatedAt = history.CreatedAt
        };
}