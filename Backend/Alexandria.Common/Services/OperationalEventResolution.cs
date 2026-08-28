using Alexandria.Data.Models;

namespace Alexandria.Common.Services;

public enum ResolveOutcome
{
    Resolved,
    AlreadyResolved,
    NotFound
}

public sealed record ResolveIncidentResult(ResolveOutcome Outcome, OperationalEvent? Event);