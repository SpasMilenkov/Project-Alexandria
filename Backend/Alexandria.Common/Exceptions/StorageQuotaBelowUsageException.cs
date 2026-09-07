namespace Alexandria.Common.Exceptions;

public class StorageQuotaBelowUsageException(long quotaBytes, long usedBytes)
    : InvalidOperationException(
        $"Storage quota ({quotaBytes} bytes) is below current usage ({usedBytes} bytes).")
{
    public long QuotaBytes { get; } = quotaBytes;
    public long UsedBytes { get; } = usedBytes;
}