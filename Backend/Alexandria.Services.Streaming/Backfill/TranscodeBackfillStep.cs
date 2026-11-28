using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Services.Streaming.Backfill;

internal sealed record TranscodeBackfillStep
{
    public required TranscodeBackfillAction Action { get; init; }

    // Only a requeue carries rungs; null keeps what the job already asks for
    public AudioRung[]? AudioRungs { get; init; }
    public VideoRung[]? VideoRungs { get; init; }
}
