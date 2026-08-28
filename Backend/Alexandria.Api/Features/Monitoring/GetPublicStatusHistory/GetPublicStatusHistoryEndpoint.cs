using Alexandria.Common.Services;
using Alexandria.Dto.Status;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Monitoring.GetPublicStatusHistory;

public class GetPublicStatusHistoryRequest
{
    public int Days { get; set; } = 90;
}

sealed class GetPublicStatusHistoryRequestValidator : Validator<GetPublicStatusHistoryRequest>
{
    public GetPublicStatusHistoryRequestValidator()
    {
        RuleFor(x => x.Days)
            .InclusiveBetween(1, 365);
    }
}

sealed class GetPublicStatusHistoryEndpoint(IOperationalEventService service)
    : Endpoint<GetPublicStatusHistoryRequest, IReadOnlyList<DailyServiceStatus>>
{
    public override void Configure()
    {
        Get("/status/history");
        AllowAnonymous();
        ResponseCache(60);
    }

    public override async Task HandleAsync(GetPublicStatusHistoryRequest req, CancellationToken ct)
    {
        var result = await service.GetPublicStatusHistoryAsync(req.Days, ct);
        await Send.OkAsync(result, ct);
    }
}