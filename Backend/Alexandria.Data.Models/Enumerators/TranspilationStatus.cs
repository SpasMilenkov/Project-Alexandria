namespace Alexandria.Data.Models.Enumerators;

public enum TranspilationStatus
{
    Queued,
    Processing,
    Partial, // some representations completed, others failed
    Ready,
    Failed,
    Cancelled,
    CancellationRequested,
}