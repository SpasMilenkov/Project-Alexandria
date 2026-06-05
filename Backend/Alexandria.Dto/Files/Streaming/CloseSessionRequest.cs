namespace Alexandria.Dto.Files.Streaming;

public sealed class CloseSessionRequest
{
    public long EndPositionSeconds { get; init; }

    // actual played seconds, reported by the client (excludes seek jumps)
    public long ListenedSeconds { get; init; }
}