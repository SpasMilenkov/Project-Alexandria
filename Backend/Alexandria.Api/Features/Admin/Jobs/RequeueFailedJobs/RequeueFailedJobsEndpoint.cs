using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.Jobs.RequeueFailedJobs;

public class RequeueFailedJobsRequest
{
    public JobType Type { get; set; }
    public int Limit { get; set; } = 100;
}

public sealed record RequeueFailedJobsResponse(int Requeued);

public class RequeueFailedJobsRequestValidator : Validator<RequeueFailedJobsRequest>
{
    public RequeueFailedJobsRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Limit).InclusiveBetween(1, 500);
    }
}

sealed class RequeueFailedJobsEndpoint(IJobRequeueService requeueService)
    : Endpoint<RequeueFailedJobsRequest, RequeueFailedJobsResponse>
{
    public override void Configure()
    {
        Post("/admin/jobs/requeue-failed");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(RequeueFailedJobsRequest req, CancellationToken ct)
    {
        var requeued = await requeueService.RequeueFailedAsync(req.Type, req.Limit, ct);
        await Send.OkAsync(new RequeueFailedJobsResponse(requeued), ct);
    }
}
