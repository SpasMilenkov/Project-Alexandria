using Common;
using Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Models;

namespace Storage.Promotions;

public class PromotionScannerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PromotionScannerWorker> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(2);

    public PromotionScannerWorker(
        IServiceProvider serviceProvider,
        ILogger<PromotionScannerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PromotionScannerWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndPromoteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during promotion scan");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }

        _logger.LogInformation("PromotionScannerWorker stopped");
    }

    private async Task ScanAndPromoteAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var promotionService = scope.ServiceProvider.GetRequiredService<IPromotionService>();

        // Find unpromoted content objects
        var unpromotedObjects = await unitOfWork.ContentObjects
            .FindAsync(co =>
                    !co.IsPromoted &&
                    co.PromotionAttempts < 10, // Don't retry failed ones
                ct);

        var contentObjects = unpromotedObjects as ContentObject[] ?? unpromotedObjects.ToArray();
        if (!contentObjects.Any())
        {
            _logger.LogDebug("No unpromoted content objects found");
            return;
        }

        _logger.LogInformation(
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