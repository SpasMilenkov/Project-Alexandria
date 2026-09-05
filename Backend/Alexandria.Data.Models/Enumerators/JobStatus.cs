namespace Alexandria.Data.Models.Enumerators;

public enum JobStatus
{
    Queued,
    Processing,
    Partial,
    Ready,
    Failed,
    Cancelled,
    CancellationRequested,
}