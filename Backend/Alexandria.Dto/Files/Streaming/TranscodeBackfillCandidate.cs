namespace Alexandria.Dto.Files.Streaming;

/// <summary>A live file in a backfill's scope, with the transcode its current version already has.</summary>
public sealed record TranscodeBackfillCandidate
{
    public required Guid FileId { get; init; }
    public required Guid VersionId { get; init; }
    public required Guid OwnerId { get; init; }
    public required string MimeType { get; init; }

    // Null when the current version was never transcoded
    public ExistingTranscode? Transcode { get; init; }
}
