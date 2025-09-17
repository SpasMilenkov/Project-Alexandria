using Common;
using Data;
using Infrastructure.Domain.Repositories;
using Infrastructure.Domain.Services;
using Infrastructure.Repositories;
using Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}