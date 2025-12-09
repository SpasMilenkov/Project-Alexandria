using Common.Repositories;

namespace Common;

public interface IUnitOfWork
{
    IFileRepository Files { get; }
    IPreviewRepository Previews { get; }
    IMediaMetadataRepository MediaMetadata { get; }
    IRefreshTokenRepository RefreshTokens { get;  }
    ITagRepository Tags { get; }
    IDirectoryRepository Directories { get; }
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    public Task CommitAsync(CancellationToken cancellationToken = default);
    public Task RollbackAsync(CancellationToken cancellationToken = default);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
    public void Dispose();
    public ValueTask DisposeAsync();
}