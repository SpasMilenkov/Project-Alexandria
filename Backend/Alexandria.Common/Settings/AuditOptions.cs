namespace Alexandria.Common.Settings;

/// <summary>Bound from the <c>Audit</c> configuration section.</summary>
public sealed class AuditOptions
{
    /// <summary>Exports stop after this many rows so one request cannot stream the whole table.</summary>
    public int ExportMaxRows { get; set; } = 50_000;

    public int RetentionSweepIntervalHours { get; set; } = 24;
}
