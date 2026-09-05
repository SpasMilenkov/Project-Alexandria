using Alexandria.Common;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data;
using Alexandria.Repositories;
using Alexandria.Services.Monitoring;
using Alexandria.Services.Storage;
using Alexandria.Services.Storage.Directories;
using Alexandria.Services.Storage.Promotions;
using Alexandria.Services.User.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Infrastructure.Workers;

public static class ServiceExtensions
{
    public static IServiceCollection AddCoreWorkerServices(this IServiceCollection services)
    {
        services.AddScoped<IStorageService, S3Service>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMediaMetadataRepository, MediaMetadataRepository>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IPreviewJobRepository, PreviewJobRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IFileVersionRepository, FileVersionRepository>();
        services.AddScoped<IContentObjectRepository, ContentObjectRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDirectoryRepository, DirectoryRepository>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IUploadRepository, UploadRepository>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<IAdminSettingsRepository, AdminSettingsRepository>();
        services.AddScoped<IStreamHistoryRepository, StreamHistoryRepository>();
        services.AddScoped<IStreamingRepresentationRepository, StreamingRepresentationRepository>();
        services.AddScoped<ITranspilationJobRepository, TranspilationJobRepository>();
        services.AddScoped<IPolicyRuleRepository, PolicyRuleRepository>();
        services.AddScoped<IDirectoryPolicyRepository, DirectoryPolicyRepository>();
        services.AddScoped<IPlaylistRepository, PlaylistRepository>();
        services.AddScoped<ISignedUrlRepository, SignedUrlRepository>();
        services.AddScoped<ITrackLyricsRepository, TrackLyricsRepository>();
        services.AddScoped<IEssentiaBatchRepository, EssentiaBatchRepository>();
        services.AddScoped<IEssentiaBatchFileRepository, EssentiaBatchFileRepository>();
        services.AddScoped<IFileEnrichmentRepository, FileEnrichmentRepository>();
        services.AddScoped<IFileTagService, FileTagService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<IOperationalEventRepository, OperationalEventRepository>();
        services.AddScoped<IJobRepository, JobRepository>();

        services.AddSingleton<PromotionQueueService>();
        services.AddSingleton<IPromotionQueue>(sp =>
            sp.GetRequiredService<PromotionQueueService>());
        services.AddSingleton<IJobOutcomeTracker, JobOutcomeTracker>();
        services.AddScoped<IPromotionService, PromotionService>();

        return services;
    }
}