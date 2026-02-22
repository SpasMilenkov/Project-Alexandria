using Data.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models;

namespace Infrastructure.DocumentWorker;

public static class WorkerDbExtensions
{
    public static IServiceCollection AddWorkerDatabase(this IServiceCollection services, IConfiguration config)
    {
        var connStr = config.GetConnectionString("AlexandriaPostgres");
        services.AddDbContext<AlexandriaDbContext>(opt => opt.UseNpgsql(connStr));
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<AlexandriaDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
