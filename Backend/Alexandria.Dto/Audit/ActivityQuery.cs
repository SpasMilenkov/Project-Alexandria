using Alexandria.Data.Models.Enumerators;
using EntityType = Alexandria.Data.Models.Enumerators.EntityType;

namespace Alexandria.Dto.Audit;

public class ActivityQuery
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public EntityType? EntityType { get; set; }
    public OperationType? OperationType { get; set; }
}