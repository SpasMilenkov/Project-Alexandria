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
        var connStr = config.GetConnectionString("AlexandriaPostgres");
        services.AddDbContext<AlexandriaDbContext>(opt => opt.UseNpgsql(connStr));
        services.AddScoped<AdminSeeder>();
        return services;
    }
}
