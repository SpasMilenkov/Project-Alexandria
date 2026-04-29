using Alexandria.Common;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data;
using Alexandria.Repositories;
using Alexandria.Services.Preview.Documents;
using Alexandria.Services.Storage;
using Alexandria.Services.Storage.Directories;
using Alexandria.Services.Storage.Promotions;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Infrastructure.DocumentWorker;

public static class ServiceExtensions
{
    public static IServiceCollection AddSWorkerServices(this IServiceCollection services)
    {
        services.AddScoped<IStorageService, S3Service>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IMediaMetadataRepository, MediaMetadataRepository>();
        services.AddScoped<IPdfPreviewService, PdfPreviewService>();
        services.AddScoped<IPreviewRepository, PreviewRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IDirectoryRepository, DirectoryRepository>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IFileVersionRepository, FileVersionRepository>();
        services.AddScoped<IContentObjectRepository, ContentObjectRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUploadRepository, UploadRepository>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<IAdminSettingsRepository, AdminSettingsRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<PromotionQueueService>();
        services.AddSingleton<IPromotionQueue>(sp =>
            sp.GetRequiredService<PromotionQueueService>());
        services.AddScoped<IPromotionService, PromotionService>();
        return services;
    }
}