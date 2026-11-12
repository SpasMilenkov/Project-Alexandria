using System.Data.Common;
using Alexandria.Common.Audit;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Alexandria.Data.Audit;

/// <summary>
/// ExecuteUpdate, ExecuteDelete, raw SQL and single-statement SaveChanges run without a
/// transaction, so the transaction interceptor never sees them and their trigger rows would lose
/// the actor. The context is set on the connection before such a write instead; Npgsql resets
/// session settings when the connection goes back to the pool. Reads skip the extra round trip.
/// </summary>
public sealed class AuditCommandInterceptor(IAuditActorAccessor actor) : DbCommandInterceptor
{
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyIfNeededAsync(command, cancellationToken);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyIfNeededAsync(command, cancellationToken);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyIfNeededAsync(command, cancellationToken);
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyIfNeeded(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ApplyIfNeeded(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        ApplyIfNeeded(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    private async Task ApplyIfNeededAsync(DbCommand command, CancellationToken ct)
    {
        if (!NeedsContext(command))
            return;

        await AuditSessionContext.ApplyAsync(command.Connection!, null, actor, isLocal: false, ct);
    }

    private void ApplyIfNeeded(DbCommand command)
    {
        if (!NeedsContext(command))
            return;

        AuditSessionContext.Apply(command.Connection!, null, actor, isLocal: false);
    }

    private static bool NeedsContext(DbCommand command) =>
        command.Transaction is null
        && command.Connection is not null
        && !AuditSqlClassifier.IsRead(command.CommandText);
}
