namespace Alexandria.Dto.Metrics;

public class StorageInfo
{
    public long DataAvailableBytes { get; set; }
    public long DataTotalBytes { get; set; }
    public long MetadataAvailableBytes { get; set; }
    public long MetadataTotalBytes { get; set; }

    /// <summary>Sum of assigned Garage role capacities across layout nodes.</summary>
    public long GarageCapacityBytes { get; set; }

    public IReadOnlyList<GarageNodeCapacity> GarageNodes { get; set; } = [];

    public double DataAvailableGB => DataAvailableBytes / 1024.0 / 1024.0 / 1024.0;
    public double DataTotalGB => DataTotalBytes / 1024.0 / 1024.0 / 1024.0;
    public double DataUsagePercentage => 100.0 * (1 - (double)DataAvailableBytes / DataTotalBytes);
    public double GarageCapacityGB => GarageCapacityBytes / 1024.0 / 1024.0 / 1024.0;
}