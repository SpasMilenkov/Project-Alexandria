using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Workers.MediaTranspilation.Handlers;

namespace Alexandria.Workers.MediaTranspilation;

public partial class TranspilationPollingWorker(
    ILogger<TranspilationPollingWorker> logger,
    IConfiguration configuration,
    IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var pollingInterval = configuration.GetValue<int>("PollingIntervalSeconds");
        LogPollingWorkerStarting(logger, pollingInterval);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(pollingInterval));

        while (await timer.WaitForNextTickAsync(ct))
        {
            await ProcessFailedJobsAsync(ct);
        }
    }

    private async Task ProcessFailedJobsAsync(CancellationToken ct)
    {
        try
        {
            Console.WriteLine("PROCESSING JOBS");
            using var scope = serviceProvider.CreateScope();
            var jobService = scope.ServiceProvider.GetRequiredService<ITranspilationJobService>();
            var handler = scope.ServiceProvider.GetRequiredService<TranspilationJobHandler>();

            var result = await jobService.FindJobsAsync(
                new TranspilationJobQuery
                {
                    Status = TranspilationStatus.Failed,
                    PageSize = 10,
                    IsSystem = true
                }, ct);
            if (result.TotalCount == 0)
            {
                LogNoPendingJobs(logger);
                return;
            }

            LogFoundFailedJobs(logger, result.TotalCount);

            foreach (var job in result.Items)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    await handler.HandleAsync(job.Id, ct);
                }
                catch (Exception ex)
                {
                    LogUnhandledJobError(logger, ex, job.Id);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPollCycleError(logger, ex);
        }
    }

    public override Task StopAsync(CancellationToken ct)
    {
        LogPollingWorkerStopping(logger);
        return base.StopAsync(ct);
    }
}