namespace Alexandria.Dto.Audit;

/// <summary>Who is reading the log. Non-admins only ever see rows they did or that affected them.</summary>
public sealed record AuditViewer(Guid UserId, bool IsAdmin);
