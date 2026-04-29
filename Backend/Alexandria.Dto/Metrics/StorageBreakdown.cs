using Alexandria.Dto.Files;

namespace Alexandria.Dto.Metrics;

public class StorageBreakdown
{
    public required Dictionary<string, long> SizeByType { get; set; }
    public required long TrashSize { get; set; }
    public required IEnumerable<FileSummary> OldFiles { get; set; }
}