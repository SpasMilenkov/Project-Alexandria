using Alexandria.Common;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data;
using Alexandria.Repositories;
using Alexandria.Services.Monitoring;
using Alexandria.Services.Preview;
using Alexandria.Services.Preview.Archives;
using Alexandria.Services.Preview.Text;
using Alexandria.Services.Storage;
using Alexandria.Services.Storage.AutoTagging;
using Alexandria.Services.Storage.Cleanup;
using Alexandria.Services.Storage.Directories;
using Alexandria.Services.Storage.Policies;
using Alexandria.Services.Storage.Promotions;
using Alexandria.Services.Storage.SignedUrls;
using Alexandria.Services.Streaming;
using Alexandria.Services.User;
using Alexandria.Services.User.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IPolicyDispatcher = Alexandria.Common.Services.IPolicyDispatcher;

namespace Alexandria.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IStorageService, S3Service>();
        services.AddScoped<PreviewSizeBackfillService>();
        services.AddScoped<RepresentationSizeBackfillService>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMediaMetadataRepository, MediaMetadataRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IArchivePreviewService, ArchivePreviewService>();
        services.AddScoped<ITextPreviewService, TextPreviewService>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IPreviewJobRepository, PreviewJobRepository>();
        services.AddScoped<IPreviewService, PreviewService>();
        services.AddScoped<IFileTagService, FileTagService>();
        services.AddScoped<IDirectoryRepository, DirectoryRepository>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IFileVersionRepository, FileVersionRepository>();
        services.AddScoped<IContentObjectRepository, ContentObjectRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUploadRepository, UploadRepository>();
        services.AddScoped<IFileService, FileService>();
        services.AddHttpClient<MetricsService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<IAdminSettingsRepository, AdminSettingsRepository>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<IAdminSettingsService, AdminSettingsService>();
        services.AddScoped<IStreamHistoryRepository, StreamHistoryRepository>();
        services.AddScoped<IStreamingRepresentationRepository, StreamingRepresentationRepository>();
        services.AddScoped<ITranspilationJobRepository, TranspilationJobRepository>();
        services.AddScoped<ITranspilationJobService, TranspilationJobService>();
        services.AddScoped<IStreamHistoryService, StreamHistoryService>();
        services.AddScoped<IStreamingRepresentationService, StreamingRepresentationService>();
        services.AddScoped<IPlaylistService, PlaylistService>();
        services.AddScoped<IPlaylistRepository, PlaylistRepository>();
        services.AddScoped<IPolicyRuleRepository, PolicyRuleRepository>();
        services.AddScoped<IDirectoryPolicyRepository, DirectoryPolicyRepository>();
        services.AddScoped<IPolicyDispatcher, PolicyDispatcher>();
        services.AddScoped<IDirectoryPolicyService, DirectoryPolicyService>();
        services.AddScoped<IJobQueue, JobQueue>();
        services.AddScoped<ISignedUrlRepository, SignedUrlRepository>();
        services.AddScoped<ISignedUrlService, SignedUrlService>();
        services.AddScoped<ITrackLyricsRepository, TrackLyricsRepository>();
        services.AddScoped<ITrackLyricsService, TrackLyricsService>();
        services.AddScoped<IEssentiaBatchRepository, EssentiaBatchRepository>();
        services.AddScoped<IEssentiaBatchFileRepository, EssentiaBatchFileRepository>();
        services.AddScoped<IFileEnrichmentRepository, FileEnrichmentRepository>();
        services.AddScoped<IOperationalEventRepository, OperationalEventRepository>();
        services.AddScoped<IOperationalEventService, OperationalEventService>();
        services.AddScoped<ITranspilationStatsService, TranspilationStatsService>();
        services.AddScoped<IPreviewStatsService, PreviewStatsService>();
        services.AddScoped<IAdminStorageStatsService, AdminStorageStatsService>();
        services.AddScoped<ILyricsStatsService, LyricsStatsService>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddSingleton<PromotionQueueService>();
        services.AddSingleton<IPromotionQueue>(sp =>
            sp.GetRequiredService<PromotionQueueService>());
        services.AddScoped<IPromotionService, PromotionService>();

        // Background Workers
        services.AddHostedService<PromotionQueueWorker>();
        services.AddHostedService<PromotionScannerWorker>();
        services.AddHostedService<TempCleanupWorker>();
        services.AddHostedService<OrphanedCleanupWorker>();
        services.AddHostedService<PreviewSizeBackfillWorker>();
        services.AddHostedService<RepresentationSizeBackfillWorker>();

        services.AddMemoryCache();

        services.AddHttpClient("worker-health")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddResourceMonitoring();
        return services;
    }

    /// <summary>
    /// Binds the auto-tagging derivation options from the <c>Tagging</c> configuration
    /// section. Values not present in config fall back to the locked defaults.
    /// </summary>
    public static IServiceCollection AddAutoTagging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AutoTaggingOptions>(configuration.GetSection("Tagging"));
        return services;
    }
}