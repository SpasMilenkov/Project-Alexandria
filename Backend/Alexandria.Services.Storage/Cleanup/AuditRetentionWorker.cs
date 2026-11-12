using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage.Cleanup;

public sealed partial class AuditRetentionWorker(
    IServiceProvider serviceProvider,
    IOptions<AuditOptions> options,
    ILogger<AuditRetentionWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.RetentionSweepIntervalHours));

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PurgeAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogPurgeFailed(logger, ex);
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            LogStopped(logger);
        }
    }

    internal async Task<int> PurgeAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<AuditContext>().RunAsSystem();

        var settings = services.GetRequiredService<IAdminSettingsService>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var auditWriter = services.GetRequiredService<IAuditWriter>();

        var retention = await settings.GetAuditRetentionAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retention.RetentionDays);

        var deleted = await unitOfWork.AuditLogs.DeleteRangeAsync(cutoff, ct);

        if (deleted == 0)
            return 0;

        auditWriter.Record(new AuditEntry
        {
            EventCode = AuditEventCode.AuditLogsPurged,
            OperationType = OperationType.Delete,
            EntityType = EntityType.System,
            Metadata = new Dictionary<string, object?>
            {
                ["deleted_count"] = deleted,
                ["cutoff"] = cutoff,
                ["retention_days"] = retention.RetentionDays,
            },
        });

        await unitOfWork.SaveChangesAsync(ct);

        LogPurged(logger, deleted, cutoff, retention.RetentionDays);
        return deleted;
    }
}
