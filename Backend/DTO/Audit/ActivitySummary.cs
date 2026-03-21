using Models.Enumerators;

namespace DTO.Audit;

public class ActivitySummary
{
    public required long TotalOperations { get; set; }
    public required Dictionary<OperationType, int> CountPerOperation { get; set; }
}
