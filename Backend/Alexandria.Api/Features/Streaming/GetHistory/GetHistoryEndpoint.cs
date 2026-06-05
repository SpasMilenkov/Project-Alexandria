using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.GetHistory;

internal sealed class GetStreamHistoryRequest
{
    public Guid? FileId { get; init; }
    public bool? Completed { get; init; }
    public DateTime? LastAccessedAfter { get; init; }
    public DateTime? LastAccessedBefore { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

internal sealed class GetStreamHistoryValidator : Validator<GetStreamHistoryRequest>
{
    public GetStreamHistoryValidator()
    {
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.LastAccessedBefore)
            .GreaterThan(x => x.LastAccessedAfter)
            .When(x => x.LastAccessedAfter.HasValue && x.LastAccessedBefore.HasValue)
            .WithMessage("LastAccessedBefore must be after LastAccessedAfter");
    }
}

internal sealed class GetStreamHistoryEndpoint(IStreamHistoryService historyService)
    : Endpoint<GetStreamHistoryRequest, PaginatedResult<StreamHistoryDto>>
{
    public override void Configure()
    {
        Get("/stream-history");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetStreamHistoryRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await historyService.FindAsync(userId, new StreamHistoryQuery
        {
            FileId = req.FileId,
            Completed = req.Completed,
            LastAccessedAfter = req.LastAccessedAfter,
            LastAccessedBefore = req.LastAccessedBefore,
            CurrentPage = req.CurrentPage,
            PageSize = req.PageSize,
        }, ct), ct);
    }
}