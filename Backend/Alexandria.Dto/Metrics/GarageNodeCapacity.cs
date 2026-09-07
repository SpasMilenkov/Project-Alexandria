namespace Alexandria.Dto.Metrics;

public class GarageNodeCapacity
{
    public required string NodeId { get; set; }
    public long RoleCapacityBytes { get; set; }
    public bool Connected { get; set; }
}