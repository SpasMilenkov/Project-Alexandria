using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

public class ActivitySummary
{
    public required long TotalOperations { get; set; }
    public required Dictionary<OperationType, int> CountPerOperation { get; set; }
}