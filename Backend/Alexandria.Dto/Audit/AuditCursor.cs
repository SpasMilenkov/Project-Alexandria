namespace Alexandria.Dto.Audit;

/// <summary>
/// A row's place in the audit log's newest-first order. The id breaks ties between rows written
/// in the same instant, so every row has exactly one position.
/// </summary>
public sealed record AuditCursor(DateTimeOffset Timestamp, Guid Id);
