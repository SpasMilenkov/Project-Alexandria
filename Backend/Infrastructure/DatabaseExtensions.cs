using Common.Audit;
using Data.Context;
using Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<AuditContext>();

        services.AddDbContext<AlexandriaDbContext>((sp, opt) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            opt.UseNpgsql(configuration.GetConnectionString("AlexandriaPostgres"));
        });
        services.AddScoped<AdminSeeder>();
        return services;
    }
}
