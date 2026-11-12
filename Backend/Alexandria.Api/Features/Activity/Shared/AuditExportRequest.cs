using Alexandria.Dto.Audit;

namespace Alexandria.Api.Features.Activity.Shared;

/// <summary>
/// An export of the filtered rows. The part bounds come from the export plan; without them the
/// export starts at the newest row.
/// </summary>
public class AuditExportRequest : AuditFilterRequest
{
    public AuditExportFormat Format { get; set; } = AuditExportFormat.Csv;

    /// <summary>The part's newest row, included.</summary>
    public DateTimeOffset? StartAt { get; set; }

    public Guid? StartId { get; set; }

    /// <summary>The next part's newest row, left out.</summary>
    public DateTimeOffset? EndAt { get; set; }

    public Guid? EndId { get; set; }

    public AuditCursor? StartCursor() => ToCursor(StartAt, StartId);

    public AuditCursor? EndCursor() => ToCursor(EndAt, EndId);

    private static AuditCursor? ToCursor(DateTimeOffset? at, Guid? id)
    {
        if (at is null || id is null)
            return null;

        return new AuditCursor(at.Value, id.Value);
    }
}
