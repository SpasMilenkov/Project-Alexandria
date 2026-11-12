namespace Alexandria.Dto.Audit;

/// <summary>
/// How an export splits into files of at most <see cref="PartSize"/> rows. Rows written after the
/// plan was made are newer than its first part, so they never shift the parts.
/// </summary>
public sealed class AuditExportPlan
{
    public required int Total { get; init; }
    public required int PartSize { get; init; }
    public required IReadOnlyList<AuditExportPart> Parts { get; init; }
}
