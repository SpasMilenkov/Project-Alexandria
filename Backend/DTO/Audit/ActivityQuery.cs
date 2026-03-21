using Models.Enumerators;
using EntityType =  Models.Enumerators.EntityType;

namespace DTO.Audit;

public class ActivityQuery
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public EntityType? EntityType { get; set; }
    public OperationType? OperationType { get; set; }
}
