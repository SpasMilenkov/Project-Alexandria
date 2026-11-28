namespace Alexandria.Services.Streaming.Backfill;

internal enum TranscodeBackfillAction
{
    Create,
    Requeue,
    Covered,
    InFlight,
    Cancelled,
    Unfinished,
}
