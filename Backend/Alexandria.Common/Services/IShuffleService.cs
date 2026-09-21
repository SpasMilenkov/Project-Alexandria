using Alexandria.Dto.Files.Streaming.Shuffle;

namespace Alexandria.Common.Services;

public interface IShuffleService
{
    Task<ShuffleSessionResponse> CreateSessionAsync(
        Guid ownerId, CreateShuffleSessionCommand command, CancellationToken ct);

    Task<ShuffleSessionResponse> GetSessionBatchAsync(
        Guid ownerId, Guid sessionId, int offset, int limit, CancellationToken ct);

    Task DeleteSessionAsync(Guid ownerId, Guid sessionId, CancellationToken ct);
}