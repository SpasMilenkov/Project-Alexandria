using Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// Background service to clean up expired/revoked refresh tokens
public partial class RefreshTokenCleanupService(
    IServiceProvider serviceProvider,
    ILogger<RefreshTokenCleanupService> logger)
    : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogRefreshTokenCleanupServiceStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokensAsync();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when application shuts down
                break;
            }
            catch
            {
                LogErrorOccurredDuringTokenCleanup();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        LogRefreshTokenCleanupServiceStopped();
    }

    private async Task CleanupExpiredTokensAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        var expiredTokens = await dbContext.RefreshTokens
            .Where(rt => rt.ExpiresAt < DateTime.UtcNow || 
                        (rt.IsRevoked && rt.RevokedAt < cutoffDate))
            .ToListAsync();

        if (expiredTokens.Any())
        {
            dbContext.RefreshTokens.RemoveRange(expiredTokens);
            await dbContext.SaveChangesAsync();

            LogCleanedUpCountExpiredRevokedRefreshTokens(expiredTokens.Count);
        }
    }

    [LoggerMessage(LogLevel.Information, "Refresh Token Cleanup Service started")]
    partial void LogRefreshTokenCleanupServiceStarted();

    [LoggerMessage(LogLevel.Error, "Error occurred during token cleanup")]
    partial void LogErrorOccurredDuringTokenCleanup();

    [LoggerMessage(LogLevel.Information, "Refresh Token Cleanup Service stopped")]
    partial void LogRefreshTokenCleanupServiceStopped();

    [LoggerMessage(LogLevel.Information, "Cleaned up {count} expired/revoked refresh tokens")]
    partial void LogCleanedUpCountExpiredRevokedRefreshTokens(int count);
}
