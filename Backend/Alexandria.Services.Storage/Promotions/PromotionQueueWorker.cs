using Alexandria.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Promotions;

public class PromotionQueueWorker(
    IServiceProvider serviceProvider,
    PromotionQueueService queue,
    ILogger<PromotionQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PromotionQueueWorker started");

        await foreach (var (contentObjectId, tempObjectKey) in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var promotionService = scope.ServiceProvider
                    .GetRequiredService<IPromotionService>();

                await promotionService.TryPromoteContentObjectAsync(
                    contentObjectId,
                    tempObjectKey,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error processing promotion for ContentObject {ContentObjectId}",
                    contentObjectId);
            }
        }

        logger.LogInformation("PromotionQueueWorker stopped");
    }
}