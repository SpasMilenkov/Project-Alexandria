using Alexandria.Common.Repositories;

namespace Alexandria.Common;

public interface IUnitOfWork : IDisposable
{
    IFileRepository Files { get; }
    IPreviewRepository Previews { get; }
    IMediaMetadataRepository MediaMetadata { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    ITagRepository Tags { get; }
    IDirectoryRepository Directories { get; }
    IFileVersionRepository FileVersions { get; }
    IContentObjectRepository ContentObjects { get; }
    IAuditLogRepository AuditLogs { get; }
    IUploadRepository Uploads { get; }
    IUserRepository Users { get; }
    IUserSettingsRepository UserSettings { get; }
    IAdminSettingsRepository AdminSettings { get; }
    IStreamingRepresentationRepository StreamingRepresentations { get; }
    ITranspilationJobRepository TranspilationJobs { get; }
    IStreamHistoryRepository StreamingHistories { get; }
    IPolicyRuleRepository PolicyRules { get; }
    IDirectoryPolicyRepository DirectoryPolicies { get; }
    IPlaylistRepository Playlists { get; }
    ISignedUrlRepository SignedUrls { get; }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    public Task CommitAsync(CancellationToken cancellationToken = default);
    public Task RollbackAsync(CancellationToken cancellationToken = default);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
}