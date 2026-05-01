using Alexandria.Data.Context;
using Alexandria.Data.Seeders;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Alexandria.Infrastructure;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AlexandriaDbContext>>();

        await MigrateWithRetryAsync(db, logger);

        var seeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
        await seeder.SeedAsync();
    }

    private static async Task MigrateWithRetryAsync(AlexandriaDbContext db, ILogger logger)
    {
        const int maxRetries = 5;
        var delay = TimeSpan.FromSeconds(2);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                return;
            }
            catch (NpgsqlException ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex,
                    "Database not ready (attempt {Attempt}/{Max}). Retrying in {Delay}s. Error: {Message}",
                    attempt, maxRetries, delay.TotalSeconds, ex.Message);

                await Task.Delay(delay);
                delay *= 2;
            }
        }
    }
}