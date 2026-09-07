using Alexandria.Dto.Files;
using Alexandria.Dto.PreviewsStats;

namespace Alexandria.Dto.Metrics;

public class StorageBreakdown
{
    public required Dictionary<string, long> SizeByType { get; set; }
    public required long TrashSize { get; set; }
    public required IEnumerable<FileSummary> OldFiles { get; set; }

    /// <summary>Sum of <see cref="SizeByType"/> values: all versions of live files.</summary>
    public long FilesSize { get; set; }

    /// <summary>Sum of owned preview artifacts on the preview bucket (live files only).</summary>
    public long PreviewsSize { get; set; }

    public int PreviewsCount { get; set; }

    public IReadOnlyList<PreviewKindTotals> PreviewsByKind { get; set; } = [];

    /// <summary>Sum of owned transcoded representations on the streaming bucket.</summary>
    public long TranscodedSize { get; set; }

    public int RepresentationsCount { get; set; }

    /// <summary>Designated quota in bytes. 0 for legacy rows until the quota backfill.</summary>
    public long QuotaBytes { get; set; }

    /// <summary>Files + previews + transcoded. Trash is reported separately.</summary>
    public long UsedBytes { get; set; }
}