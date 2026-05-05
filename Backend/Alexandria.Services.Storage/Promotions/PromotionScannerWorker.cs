using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Promotions;

public class PromotionScannerWorker(
    IServiceProvider serviceProvider,
    ILogger<PromotionScannerWorker> logger) : BackgroundService
{
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PromotionScannerWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndPromoteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during promotion scan");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }

        logger.LogInformation("PromotionScannerWorker stopped");
    }

    private async Task ScanAndPromoteAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var promotionService = scope.ServiceProvider.GetRequiredService<IPromotionService>();

        var unpromotedObjects = await unitOfWork.ContentObjects
            .FindAsync(co =>
                    !co.IsPromoted &&
                    co.PromotionAttempts < 10, // Don't retry failed ones
                ct);

        var contentObjects = unpromotedObjects as ContentObject[] ?? [.. unpromotedObjects];
        if (contentObjects.Length == 0)
        {
            logger.LogDebug("No unpromoted content objects found");
            return;
        }

        logger.LogInformation(
            "Found {Count} unpromoted content objects, attempting promotion",
            contentObjects.Count());

        foreach (var contentObject in contentObjects)
        {
            if (ct.IsCancellationRequested || contentObject.Upload is null)
                break;

            await promotionService.TryPromoteContentObjectAsync(
                contentObject.Id,
                contentObject.Upload.TempObjectKey,
                ct);
        }
    }
}