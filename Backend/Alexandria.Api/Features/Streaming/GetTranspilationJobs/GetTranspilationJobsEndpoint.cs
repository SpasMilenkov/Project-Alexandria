using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.GetTranspilationJobs;

internal sealed class GetTranspilationJobsRequest
{
    public TranspilationStatus? Status { get; init; }
    public bool? IsVideo { get; init; }
    public Guid? VersionId { get; init; }
    public DateTime? CreatedAfter { get; init; }
    public DateTime? CreatedBefore { get; init; }
    public DateTime? CompletedAfter { get; init; }
    public DateTime? CompletedBefore { get; init; }
    public int? MinRetryCount { get; init; }
    public int CurrentPage { get; init; } = 0;
    public int PageSize { get; init; } = 25;
}

internal sealed class GetTranspilationJobsRequestValidator : Validator<GetTranspilationJobsRequest>
{
    public GetTranspilationJobsRequestValidator()
    {
        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.MinRetryCount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinRetryCount.HasValue);

        RuleFor(x => x.CreatedBefore)
            .GreaterThan(x => x.CreatedAfter)
            .When(x => x.CreatedAfter.HasValue && x.CreatedBefore.HasValue)
            .WithMessage("CreatedBefore must be after CreatedAfter");

        RuleFor(x => x.CompletedBefore)
            .GreaterThan(x => x.CompletedAfter)
            .When(x => x.CompletedAfter.HasValue && x.CompletedBefore.HasValue)
            .WithMessage("CompletedBefore must be after CompletedAfter");
    }
}

internal sealed class GetTranspilationJobsEndpoint(ITranspilationJobService jobService, IFileService fileService)
    : Endpoint<GetTranspilationJobsRequest, PaginatedResult<TranspilationJobWithDetailsDto>>
{
    public override void Configure()
    {
        Get("/streaming/jobs");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetTranspilationJobsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        if (!req.VersionId.HasValue)
        {
            await Send.OkAsync(await jobService.GetWithDetailsAsync(new TranspilationJobQuery
            {
                UserId = userId,
                Status = req.Status,
                IsVideo = req.IsVideo,
                CreatedAfter = req.CreatedAfter,
                CreatedBefore = req.CreatedBefore,
                CompletedAfter = req.CompletedAfter,
                CompletedBefore = req.CompletedBefore,
                MinRetryCount = req.MinRetryCount,
                CurrentPage = req.CurrentPage,
                PageSize = req.PageSize
            }, ct), ct);
            return;
        }

        var isVideo =
            await fileService.IsVideo(req.VersionId.Value, userId, ct);

        var result = await jobService.GetWithDetailsAsync(new TranspilationJobQuery
        {
            UserId = userId,
            Status = req.Status,
            IsVideo = isVideo,
            VersionId = req.VersionId,
            CreatedAfter = req.CreatedAfter,
            CreatedBefore = req.CreatedBefore,
            CompletedAfter = req.CompletedAfter,
            CompletedBefore = req.CompletedBefore,
            MinRetryCount = req.MinRetryCount,
            CurrentPage = req.CurrentPage,
            PageSize = req.PageSize
        }, ct);

        await Send.OkAsync(result, ct);
    }
}