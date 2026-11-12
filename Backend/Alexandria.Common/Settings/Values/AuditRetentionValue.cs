using System.ComponentModel.DataAnnotations;

namespace Alexandria.Common.Settings.Values;

public class AuditRetentionValue
{
    public const int MinDays = 30;
    public const int MaxDays = 3650;
    public const int DefaultDays = 730;

    [Range(MinDays, MaxDays)]
    public int RetentionDays { get; set; } = DefaultDays;
}
