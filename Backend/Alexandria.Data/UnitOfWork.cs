using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Microsoft.EntityFrameworkCore.Storage;

namespace Alexandria.Data;

public sealed class UnitOfWork(
    IFileRepository files,
    IPreviewRepository previews,
    IMediaMetadataRepository mediaData,
    IRefreshTokenRepository refreshTokens,
    ITagRepository tags,
    IDirectoryRepository directories,
    IFileVersionRepository fileVersions,
    IContentObjectRepository contentObjects,
    IAuditLogRepository auditLogs,
    IUploadRepository uploads,
    IUserRepository users,
    IUserSettingsRepository userSettings,
    IAdminSettingsRepository adminSettings,
    IStreamingRepresentationRepository streamingRepresentations,
    ITranspilationJobRepository transpilationJobs,
    IStreamHistoryRepository streamingHistories,
    AlexandriaDbContext dbContext) : IUnitOfWork
{
    public IFileRepository Files { get; } = files;
    public IPreviewRepository Previews { get; } = previews;
    public IMediaMetadataRepository MediaMetadata { get; } = mediaData;
    public IRefreshTokenRepository RefreshTokens { get; } = refreshTokens;
    public ITagRepository Tags { get; } = tags;
    public IDirectoryRepository Directories { get; } = directories;
    public IFileVersionRepository FileVersions { get; } = fileVersions;
    public IContentObjectRepository ContentObjects { get; } = contentObjects;
    public IAuditLogRepository AuditLogs { get; } = auditLogs;
    public IUploadRepository Uploads { get; } = uploads;
    public IUserRepository Users { get; } = users;
    public IUserSettingsRepository UserSettings { get; } = userSettings;
    public IAdminSettingsRepository AdminSettings { get; } = adminSettings;
    public IStreamingRepresentationRepository StreamingRepresentations { get; } = streamingRepresentations;
    public ITranspilationJobRepository TranspilationJobs { get; } = transpilationJobs;
    public IStreamHistoryRepository StreamingHistories { get; } = streamingHistories;

    private IDbContextTransaction? _transaction;

    private bool _disposed;

    private int _transactionCount;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // If this is the first transaction request, create a new transaction
        if (_transactionCount == 0)
        {
            _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        // Increment the transaction counter
        _transactionCount++;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Decrement the transaction counter
        _transactionCount--;

        // Only commit when all nested transactions are complete
        if (_transactionCount == 0)
        {
            try
            {
                await SaveChangesAsync(cancellationToken);
                await _transaction!.CommitAsync(cancellationToken);
            }
            finally
            {
                await _transaction!.DisposeAsync();
                _transaction = null;
            }
        }
    }


    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;

        // Reset the counter since we're rolling back
        _transactionCount = 0;

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            dbContext.Dispose();
            _transaction?.Dispose();
        }

        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;

        await dbContext.DisposeAsync();
        if (_transaction is not null)
            await _transaction.DisposeAsync();

        _disposed = true;
    }
}