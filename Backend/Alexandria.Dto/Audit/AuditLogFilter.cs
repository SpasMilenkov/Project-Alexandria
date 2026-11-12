using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

/// <summary>
/// Repository-level filter shared by the personal history, admin explorer, entity history and
/// exports. Every set property narrows the result.
/// </summary>
public sealed record AuditLogFilter
{
    /// <summary>Rows the user did or that affected their data (UserId or OwnerId).</summary>
    public Guid? ScopeUserId { get; init; }

    /// <summary>
    /// Who is reading. Other accounts' personal content (<see cref="AuditPersonalContent"/>) is
    /// left out unless the reader did it; account, security and ownerless rows pass.
    /// </summary>
    public Guid? ReaderId { get; init; }

    /// <summary>
    /// Leaves out personal content entirely, the reader's own included. The admin explorer is about
    /// accounts and the system; each account's library history lives in its own activity page.
    /// </summary>
    public bool ExcludePersonalContent { get; init; }

    public Guid? UserId { get; init; }
    public Guid? OwnerId { get; init; }
    public EntityType? EntityType { get; init; }
    public Guid? EntityId { get; init; }

    /// <summary>Also match rows whose ParentEntityId is <see cref="EntityId"/> (versions, previews, tags of a file).</summary>
    public bool IncludeChildren { get; init; }

    public IReadOnlyCollection<AuditEventCode>? EventCodes { get; init; }
    public OperationType? OperationType { get; init; }
    public LogSource? Source { get; init; }
    public string? IpAddress { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public Guid? CorrelationId { get; init; }

    /// <summary>Only this row and older ones, in newest-first order.</summary>
    public AuditCursor? UpTo { get; init; }

    /// <summary>Only rows newer than this one, which itself is left out.</summary>
    public AuditCursor? After { get; init; }
}
