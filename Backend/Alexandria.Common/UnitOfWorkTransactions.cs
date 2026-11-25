namespace Alexandria.Common;

public static class UnitOfWorkTransactions
{
    /// <summary>
    /// Runs <paramref name="work"/> in a unit-of-work transaction: committed when it finishes,
    /// rolled back when it throws. Outbox messages enqueued inside commit with the rest.
    /// </summary>
    public static async Task InTransactionAsync(this IUnitOfWork unitOfWork, Func<Task> work, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            await work();
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <inheritdoc cref="InTransactionAsync(IUnitOfWork, Func{Task}, CancellationToken)"/>
    public static async Task<T> InTransactionAsync<T>(
        this IUnitOfWork unitOfWork, Func<Task<T>> work, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var result = await work();
            await unitOfWork.CommitAsync(ct);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
