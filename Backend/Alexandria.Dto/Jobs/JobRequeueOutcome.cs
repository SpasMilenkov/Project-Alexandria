namespace Alexandria.Dto.Jobs;

public enum JobRequeueOutcome
{
    Requeued,
    NotFound,
    NotEligible,
    Unsupported,
    SourceUnavailable
}
