using Alexandria.Common.Audit;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Infrastructure.DocumentWorker;

public static class WorkerDbExtensions
{
    public static IServiceCollection AddWorkerDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<AuditContext>();

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