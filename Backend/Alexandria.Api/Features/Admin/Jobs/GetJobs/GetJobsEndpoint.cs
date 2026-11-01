using Alexandria.Common;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Jobs;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Jobs.GetJobs;

public class GetJobsRequest
{
    public JobType? Type { get; set; }
    public JobStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetJobsRequestValidator : Validator<GetJobsRequest>
{
    public GetJobsRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

sealed class GetJobsEndpoint(IUnitOfWork unitOfWork)
    : Endpoint<GetJobsRequest, PaginatedResult<AdminJobDto>>
{
    public override void Configure()
    {
        Get("/admin/jobs");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(GetJobsRequest req, CancellationToken ct)
    {
        var page = await unitOfWork.Jobs.FindAdminJobsAsync(new JobQuery
        {
            Type = req.Type,
            Status = req.Status,
            Page = req.Page,
            PageSize = req.PageSize
        }, ct);

        await Send.OkAsync(page, ct);
    }
}
