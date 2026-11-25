using Alexandria.Common.Repositories;

namespace Alexandria.Common;

public interface IUnitOfWork : IDisposable
{
    IFileRepository Files { get; }
    IPreviewRepository Previews { get; }
    IPreviewJobRepository PreviewJobs { get; }
    IMediaMetadataRepository MediaMetadata { get; }
    ICaptureMetadataRepository CaptureMetadata { get; }
    IDocumentMetadataRepository DocumentMetadata { get; }
    IDocumentTextRepository DocumentText { get; }
    IFileDraftRepository FileDrafts { get; }
    INoteLinkRepository NoteLinks { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    ITagRepository Tags { get; }
    IDirectoryRepository Directories { get; }
    IFileVersionRepository FileVersions { get; }
    IContentObjectRepository ContentObjects { get; }
    IAuditLogRepository AuditLogs { get; }
    IUploadRepository Uploads { get; }
    IUserRepository Users { get; }
    IUserSettingsRepository UserSettings { get; }
    IHomeWidgetRepository HomeWidgets { get; }
    IAdminSettingsRepository AdminSettings { get; }
    IStreamingRepresentationRepository StreamingRepresentations { get; }
    ITranspilationJobRepository TranspilationJobs { get; }
    IStreamHistoryRepository StreamingHistories { get; }
    IPolicyRuleRepository PolicyRules { get; }
    IDirectoryPolicyRepository DirectoryPolicies { get; }
    IPlaylistRepository Playlists { get; }
    ISignedUrlRepository SignedUrls { get; }
    ITrackLyricsRepository Lyrics { get; }
    IEssentiaBatchRepository EssentiaBatches { get; }
    IEssentiaBatchFileRepository EssentiaBatchFiles { get; }
    IFileEnrichmentRepository FileEnrichments { get; }
    IOperationalEventRepository OperationalEvents { get; }
    IJobRepository Jobs { get; }
    IOverviewSummaryRepository OverviewSummaries { get; }
    IOutboxMessageRepository OutboxMessages { get; }

    /// <summary>True between <see cref="BeginTransactionAsync"/> and the matching commit or rollback.</summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Runs <paramref name="action"/> once the outermost transaction has committed. It is dropped
    /// if the transaction rolls back or the commit fails.
    /// </summary>
    void AfterCommit(Action action);

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    public Task CommitAsync(CancellationToken cancellationToken = default);

    public Task RollbackAsync(CancellationToken cancellationToken = default);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
}