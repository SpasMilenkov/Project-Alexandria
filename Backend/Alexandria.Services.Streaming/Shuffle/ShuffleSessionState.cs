using System.Collections.Immutable;
using Alexandria.Dto.Files.Streaming.Shuffle;

namespace Alexandria.Services.Streaming.Shuffle;

internal sealed record ShuffleSessionState(
    Guid SessionId,
    Guid OwnerId,
    PlaybackSourceDto Source,
    DateTime CreatedAtUtc,
    int AlgorithmVersion,
    string RequestFingerprint,
    Guid RequestId,
    int? AnchorPosition,
    ImmutableArray<PlaybackSourceEntryRef> Order)
{
    public int TotalCount => Order.Length;
}