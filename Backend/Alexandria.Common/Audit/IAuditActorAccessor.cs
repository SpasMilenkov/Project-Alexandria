namespace Alexandria.Common.Audit;

/// <summary>
/// The single source of who is acting, from where, and in which request. Shared by the database
/// session context that triggers read and by manual audit entries, so both always agree.
/// </summary>
public interface IAuditActorAccessor
{
    /// <summary>The acting user, or null when no user is behind the work (workers, anonymous requests).</summary>
    Guid? UserId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    /// <summary>Stable for the whole request or worker message scope.</summary>
    Guid CorrelationId { get; }
}
