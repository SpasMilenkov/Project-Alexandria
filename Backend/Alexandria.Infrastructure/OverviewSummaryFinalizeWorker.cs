using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Summaries;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Infrastructure;

public partial class OverviewSummaryFinalizeWorker(
    IServiceProvider serviceProvider,
    ILogger<OverviewSummaryFinalizeWorker> logger)
    : BackgroundService
{
    private const int FirstWrappedYear = 2020;
    private readonly TimeSpan _sweepInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogFinalizeWorkerStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FinalizeMissingYearsAsync(stoppingToken);
                await Task.Delay(_sweepInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch
            {
                LogErrorDuringFinalizeSweep();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        LogFinalizeWorkerStopped();
    }

    private async Task FinalizeMissingYearsAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var summaries = scope.ServiceProvider.GetRequiredService<IOverviewSummaryService>();
        var repository = scope.ServiceProvider.GetRequiredService<IOverviewSummaryRepository>();

        var page = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await users.GetUsersAsync(new UserQueryDto { Page = page, PageSize = 100 }, ct);

            if (batch.Items.Count == 0)
            {
                break;
            }

            foreach (var user in batch.Items)
            {
                for (var year = DateTime.UtcNow.Year - 1; year >= FirstWrappedYear; year--)
                {
                    try
                    {
                        await summaries.FinalizeAsync(user.Id, SummaryKind.Wrapped, SummaryPeriod.ForYear(year), ct);
                    }
                    catch (Exception ex)
                    {
                        LogFinalizeFailedForUser(user.Id, ex.Message);
                    }
                }

                try
                {
                    await repository.DeleteAllProvisionalAsync(user.Id, SummaryKind.Wrapped, ct);
                }
                catch (Exception ex)
                {
                    LogFinalizeFailedForUser(user.Id, ex.Message);
                }
            }

            if (batch.Items.Count < 100)
            {
                break;
            }

            page++;
        }
    }

    [LoggerMessage(LogLevel.Information, "Overview summary finalize worker started")]
    partial void LogFinalizeWorkerStarted();

    [LoggerMessage(LogLevel.Error, "Error occurred during overview summary finalize sweep")]
    partial void LogErrorDuringFinalizeSweep();

    [LoggerMessage(LogLevel.Warning, "Finalize failed for user {UserId}: {Reason}")]
    partial void LogFinalizeFailedForUser(Guid userId, string reason);

    [LoggerMessage(LogLevel.Information, "Overview summary finalize worker stopped")]
    partial void LogFinalizeWorkerStopped();
}
