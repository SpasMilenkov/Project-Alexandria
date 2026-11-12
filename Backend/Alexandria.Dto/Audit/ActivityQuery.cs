using Alexandria.Data.Models.Enumerators;
using EntityType = Alexandria.Data.Models.Enumerators.EntityType;

namespace Alexandria.Dto.Audit;

public class ActivityQuery
{
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public EntityType? EntityType { get; set; }
    public OperationType? OperationType { get; set; }
}
