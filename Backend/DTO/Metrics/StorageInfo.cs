namespace DTO.Metrics;

public class StorageInfo
{
    public long DataAvailableBytes { get; set; }
    public long DataTotalBytes { get; set; }
    public long MetadataAvailableBytes { get; set; }
    public long MetadataTotalBytes { get; set; }

    public double DataAvailableGB => DataAvailableBytes / 1024.0 / 1024.0 / 1024.0;
    public double DataTotalGB => DataTotalBytes / 1024.0 / 1024.0 / 1024.0;
    public double DataUsagePercentage => 100.0 * (1 - (double)DataAvailableBytes / DataTotalBytes);
}
