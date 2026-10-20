using Alexandria.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

/// <summary>
/// Session-level PostgreSQL advisory lock for one playlist owner. The lock lives
/// on a dedicated open connection, so disposal must always run: it releases the
/// lock and closes the connection on every path, including acquisition failure.
/// </summary>
internal sealed class PlaylistOwnerSyncLock(AlexandriaDbContext context, long key) : IAsyncDisposable
{
    private int _disposed;

    public static async Task<IAsyncDisposable> AcquireAsync(
        AlexandriaDbContext context, Guid ownerId, CancellationToken ct)
    {
        var key = KeyFor(ownerId);

        await context.Database.OpenConnectionAsync(ct);

        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock({key})", ct);
        }
        catch
        {
            // A cancelled or failed command may still have taken the lock on the
            // server, so release it before the connection goes back to the pool.
            await TryUnlockAsync(context, key);
            await CloseQuietlyAsync(context);
            throw;
        }

        return new PlaylistOwnerSyncLock(context, key);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await TryUnlockAsync(context, key);
        await CloseQuietlyAsync(context);
    }

    private static async Task TryUnlockAsync(AlexandriaDbContext context, long key)
    {
        try
        {
            // Cancellation is ignored on purpose: the unlock must run even when the
            // caller's token already fired. If the lock was never taken, Postgres
            // answers false with a warning, not an error.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_unlock({key})", CancellationToken.None);
        }
        catch
        {
            // A broken session has already released the lock.
        }
    }

    // XOR the GUID halves into one stable 64-bit key. A collision only makes two
    // unrelated owners wait for each other; it never merges their data.
    private static long KeyFor(Guid ownerId)
    {
        var bytes = ownerId.ToByteArray();
        return BitConverter.ToInt64(bytes, 0) ^ BitConverter.ToInt64(bytes, 8);
    }

    private static async Task CloseQuietlyAsync(AlexandriaDbContext context)
    {
        try
        {
            await context.Database.CloseConnectionAsync();
        }
        catch
        {
            // The pool owns recovery from a broken connection.
        }
    }
}