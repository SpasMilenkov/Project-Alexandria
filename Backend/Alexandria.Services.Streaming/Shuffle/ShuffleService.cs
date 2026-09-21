using System.Collections.Immutable;
using System.Diagnostics;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Shuffle;

public sealed partial class ShuffleService(
    IUnitOfWork unitOfWork,
    ShuffleSessionStore store,
    TimeProvider timeProvider,
    ILogger<ShuffleService> logger) : IShuffleService
{
    private const int AlgorithmVersion = 1;
    private const int MinBatchLimit = 1;
    private const int MaxBatchLimit = 200;

    public async Task<ShuffleSessionResponse> CreateSessionAsync(
        Guid ownerId, CreateShuffleSessionCommand command, CancellationToken ct)
    {
        var source = new PlaybackSourceDto
        {
            IsVideo = command.Source.IsVideo,
            PlaylistId = command.Source.PlaylistId,
        };

        if (source.PlaylistId.HasValue
            && !await unitOfWork.Playlists.IsOwnerAsync(source.PlaylistId.Value, ownerId, ct))
            throw new PlaylistNotFoundException(source.PlaylistId.Value);

        var limit = Math.Clamp(command.Limit, MinBatchLimit, MaxBatchLimit);
        var fingerprint = BuildFingerprint(
            source, command.AnchorFileId, command.AnchorPlaylistItemId, command.AvoidFirstFileId, limit);

        var state = await store.GetOrCreateAsync(
            ownerId,
            command.RequestId,
            fingerprint,
            () => CreateStateAsync(ownerId, source, command, fingerprint, ct),
            ct);

        var batch = await HydrateBatchAsync(ownerId, source, state.Order, 0, limit, ct);
        return BuildResponse(state, batch.Items, 0, batch.ScannedCount, batch.NextOffset,
            new DateTimeOffset(timeProvider.GetUtcNow().UtcDateTime, TimeSpan.Zero) + ShuffleSessionStore.IdleTimeout);
    }

    public async Task<ShuffleSessionResponse> GetSessionBatchAsync(
        Guid ownerId, Guid sessionId, int offset, int limit, CancellationToken ct)
    {
        if (!store.TryGet(sessionId, ownerId, out var state, out var lastAccessedUtc) || state is null)
            throw new ShuffleSessionNotFoundException(sessionId);

        if (state.Source.PlaylistId.HasValue
            && !await unitOfWork.Playlists.IsOwnerAsync(state.Source.PlaylistId.Value, ownerId, ct))
        {
            store.Remove(sessionId, ownerId);
            throw new ShuffleSessionNotFoundException(sessionId);
        }

        if (offset > state.TotalCount)
            throw new ShuffleOffsetOutOfRangeException(offset, state.TotalCount);

        limit = Math.Clamp(limit, MinBatchLimit, MaxBatchLimit);
        var entries = await HydrateBatchAsync(ownerId, state.Source, state.Order, offset, limit, ct);
        return BuildResponse(state, entries.Items, offset, entries.ScannedCount, entries.NextOffset,
            new DateTimeOffset(lastAccessedUtc, TimeSpan.Zero) + ShuffleSessionStore.IdleTimeout);
    }

    public Task DeleteSessionAsync(Guid ownerId, Guid sessionId, CancellationToken ct)
    {
        return store.Remove(sessionId, ownerId)
            ? Task.CompletedTask
            : throw new ShuffleSessionNotFoundException(sessionId);
    }

    private async Task<ShuffleSessionState> CreateStateAsync(
        Guid ownerId,
        PlaybackSourceDto source,
        CreateShuffleSessionCommand command,
        string fingerprint,
        CancellationToken ct)
    {
        var asOfUtc = timeProvider.GetUtcNow().UtcDateTime;
        var elapsed = Stopwatch.StartNew();

        var candidates = await unitOfWork.Files.GetShuffleCandidatesAsync(ownerId, source, ct);
        IReadOnlyList<ShuffleListenRow> listenRows = source.IsVideo
            ? []
            : await unitOfWork.StreamingHistories.GetShuffleListenRowsAsync(
                ownerId, candidates.Select(c => c.Entry.FileId).Distinct().ToList(), asOfUtc, ct);
        var queryMs = elapsed.ElapsedMilliseconds;

        if (command.AnchorFileId.HasValue
            && !ContainsAnchor(candidates, command.AnchorFileId.Value, command.AnchorPlaylistItemId))
            throw new ShuffleAnchorConflictException();

        var order = ShuffleRanker.Rank(
            candidates, listenRows, asOfUtc, source.IsVideo, Random.Shared,
            command.AnchorFileId, command.AnchorPlaylistItemId, command.AvoidFirstFileId);
        var rankMs = elapsed.ElapsedMilliseconds - queryMs;

        LogSessionCreated(candidates.Count, queryMs, rankMs, source.IsVideo,
            source.PlaylistId.HasValue);

        return new ShuffleSessionState(
            Guid.NewGuid(),
            ownerId,
            source,
            asOfUtc,
            AlgorithmVersion,
            fingerprint,
            command.RequestId,
            command.AnchorFileId.HasValue ? 0 : null,
            order.ToImmutableArray());
    }

    private static bool ContainsAnchor(
        IReadOnlyList<ShuffleCandidate> candidates, Guid anchorFileId, Guid? anchorPlaylistItemId) =>
        anchorPlaylistItemId.HasValue
            ? candidates.Any(c => c.Entry.FileId == anchorFileId
                                  && c.Entry.PlaylistItemId == anchorPlaylistItemId.Value)
            : candidates.Any(c => c.Entry.FileId == anchorFileId);

    private async Task<(IReadOnlyList<ShuffleEntryDto> Items, int ScannedCount, int? NextOffset)> HydrateBatchAsync(
        Guid ownerId,
        PlaybackSourceDto source,
        ImmutableArray<PlaybackSourceEntryRef> order,
        int offset,
        int limit,
        CancellationToken ct)
    {
        var slice = order.Skip(offset).Take(limit).ToList();
        if (slice.Count == 0)
            return ([], 0, null);

        var dtos = await unitOfWork.Files.GetPlaybackEntriesAsync(ownerId, source, slice, ct);

        Dictionary<Guid, MediaFileDto> byKey = source.PlaylistId.HasValue
            ? dtos.Where(d => d.PlaylistItemId.HasValue).ToDictionary(d => d.PlaylistItemId!.Value)
            : dtos.ToDictionary(d => d.FileId);

        var items = new List<ShuffleEntryDto>(slice.Count);
        for (var i = 0; i < slice.Count; i++)
        {
            var key = source.PlaylistId.HasValue ? slice[i].PlaylistItemId!.Value : slice[i].FileId;
            if (byKey.TryGetValue(key, out var dto))
                items.Add(new ShuffleEntryDto { Position = offset + i, File = dto });
        }

        var scannedCount = slice.Count;
        int? nextOffset = offset + scannedCount < order.Length ? offset + scannedCount : null;
        return (items, scannedCount, nextOffset);
    }

    private static ShuffleSessionResponse BuildResponse(
        ShuffleSessionState state,
        IReadOnlyList<ShuffleEntryDto> items,
        int offset,
        int scannedCount,
        int? nextOffset,
        DateTimeOffset expiresAt)
    {
        return new ShuffleSessionResponse
        {
            SessionId = state.SessionId,
            Source = state.Source,
            AlgorithmVersion = state.AlgorithmVersion,
            CreatedAt = new DateTimeOffset(state.CreatedAtUtc, TimeSpan.Zero),
            ExpiresAt = expiresAt,
            TotalCount = state.TotalCount,
            AnchorPosition = state.AnchorPosition,
            Offset = offset,
            ScannedCount = scannedCount,
            NextOffset = nextOffset,
            Items = items,
        };
    }

    private static string BuildFingerprint(
        PlaybackSourceDto source,
        Guid? anchorFileId,
        Guid? anchorPlaylistItemId,
        Guid? avoidFirstFileId,
        int limit) =>
        string.Join('|',
            "v1",
            source.IsVideo ? "video" : "audio",
            source.PlaylistId?.ToString("D").ToLowerInvariant() ?? "-",
            anchorFileId?.ToString("D").ToLowerInvariant() ?? "-",
            anchorPlaylistItemId?.ToString("D").ToLowerInvariant() ?? "-",
            avoidFirstFileId?.ToString("D").ToLowerInvariant() ?? "-",
            limit.ToString());
}