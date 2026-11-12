using Alexandria.Dto.Audit;

namespace Alexandria.Common.Audit;

public interface IAuditWriter
{
    /// <summary>
    /// Stages an entry in the current unit of work without saving, so it commits together with the
    /// business change it describes. Callers that make no other tracked change save afterwards.
    /// </summary>
    void Record(AuditEntry entry);
}
