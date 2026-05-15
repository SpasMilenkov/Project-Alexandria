using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetHistory;

internal sealed class GetHistoryRequest
{
    public Guid? FileId { get; init; }
    public bool? Completed { get; init; }
    public DateTime? AccessedAfter { get; init; }
    public DateTime? AccessedBefore { get; init; }
    public int CurrentPage { get; init; } = 0;
    public int PageSize { get; init; } = 25;
}

internal sealed class GetHistoryEndpoint(IStreamHistoryService historyService)
    : Endpoint<GetHistoryRequest, PaginatedResult<StreamHistoryResponse>>
{
    public override void Configure()
    {
        Get("/streaming/history");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetHistoryRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await historyService.FindHistoryAsync(new StreamHistoryQuery
        {
            UserId = userId,
            FileId = req.FileId,
            Completed = req.Completed,
            AccessedAfter = req.AccessedAfter,
            AccessedBefore = req.AccessedBefore,
            CurrentPage = req.CurrentPage,
            PageSize = req.PageSize
        }, ct), cancellation: ct);
    }
}