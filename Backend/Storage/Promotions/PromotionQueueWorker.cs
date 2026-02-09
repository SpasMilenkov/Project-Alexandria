using Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Storage.Promotions;

public class PromotionQueueWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PromotionQueueService _queue;
    private readonly ILogger<PromotionQueueWorker> _logger;

    public PromotionQueueWorker(
        IServiceProvider serviceProvider,
        PromotionQueueService queue,
        ILogger<PromotionQueueWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PromotionQueueWorker started");

        await foreach (var (contentObjectId, tempObjectKey) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var promotionService = scope.ServiceProvider
                    .GetRequiredService<IPromotionService>();

                await promotionService.TryPromoteContentObjectAsync(
                    contentObjectId,
                    tempObjectKey,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing promotion for ContentObject {ContentObjectId}",
                    contentObjectId);
                // Continue processing other items
            }
        }

        _logger.LogInformation("PromotionQueueWorker stopped");
    }
}