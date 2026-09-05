using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Policies;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Jobs;
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
            using var scope = serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var handler = scope.ServiceProvider.GetRequiredService<TranspilationJobHandler>();

            var result = await unitOfWork.Jobs.FindJobsAsync(
                new JobQuery
                {
                    Status = JobStatus.Failed,
                    Type = JobType.Transpilation,
                    TriggeredByUserId = SystemConfig.SystemId,
                    MaxRetryCount = JobRetryPolicy.MaxAutoRetries,
                    PageSize = 10
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