using System.Data.Common;
using Alexandria.Common.Audit;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Alexandria.Data.Audit;

/// <summary>
/// Scopes the audit session context to each transaction, including the implicit one SaveChanges
/// opens for multi-statement batches.
/// </summary>
public sealed class AuditTransactionInterceptor(IAuditActorAccessor actor) : DbTransactionInterceptor
{
    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        await AuditSessionContext.ApplyAsync(connection, result, actor, isLocal: true, cancellationToken);
        return await base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
    }

    public override DbTransaction TransactionStarted(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result)
    {
        AuditSessionContext.Apply(connection, result, actor, isLocal: true);
        return base.TransactionStarted(connection, eventData, result);
    }
}
