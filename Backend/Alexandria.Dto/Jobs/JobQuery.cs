using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Jobs;

public class JobQuery
{
    public JobStatus? Status { get; set; }
    public JobType? Type { get; set; }
    public Guid? TriggeredByUserId { get; set; }
    public int? MaxRetryCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}