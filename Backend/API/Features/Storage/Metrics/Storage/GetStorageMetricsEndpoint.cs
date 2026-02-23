using DTO.Metrics;
using FastEndpoints;
using Storage;

namespace API.Features.Storage.Metrics.Storage;

public class GetStorageMetricsEndpoint(MetricsService metricsService):  EndpointWithoutRequest<StorageInfo>
{
    public override void Configure()
    {
        Get("/storage/available");
        Summary(s =>
        {
            s.Summary = "Get available storage from Garage";
            s.Description = "Returns storage metrics from Garage HQ";
            s.Response<StorageInfo>(200, "Storage information retrieved successfully");
            s.Response(500, "Internal server error");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var storageInfo = await metricsService.GetStorageInfoAsync();
            await Send.OkAsync(storageInfo, ct);
        }
        catch (Exception ex)
        {
            await Send.ErrorsAsync(500, ct);
            ThrowError(ex.Message);
        }
    }
}