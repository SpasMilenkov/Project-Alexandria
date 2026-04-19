using Common;
using Common.Queues;
using Common.Repositories;
using Common.Services;
using Data;
using Microsoft.Extensions.DependencyInjection;
using PreviewService.Documents;
using Repositories;
using Storage;
using Storage.Directories;
using Storage.Promotions;
using User.Service;

namespace Infrastructure.DocumentWorker;

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