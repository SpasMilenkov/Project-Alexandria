using Alexandria.Common.Services;
using Alexandria.Dto.Jobs;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Jobs.RequeueJob;

public class RequeueJobRequest
{
    public Guid JobId { get; set; }
}

public sealed record RequeueJobResponse(Guid JobId, JobRequeueOutcome Outcome);

sealed class RequeueJobEndpoint(IJobRequeueService requeueService)
    : Endpoint<RequeueJobRequest, RequeueJobResponse>
{
    public override void Configure()
    {
        Post("/admin/jobs/{JobId}/requeue");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(RequeueJobRequest req, CancellationToken ct)
    {
        var outcome = await requeueService.RequeueAsync(req.JobId, ct);

        if (outcome == JobRequeueOutcome.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (outcome == JobRequeueOutcome.Requeued)
        {
            await Send.OkAsync(new RequeueJobResponse(req.JobId, outcome), ct);
            return;
        }

        AddError(x => x.JobId, ConflictMessage(outcome));
        await Send.ErrorsAsync(409, ct);
    }

    private static string ConflictMessage(JobRequeueOutcome outcome) => outcome switch
    {
        JobRequeueOutcome.NotEligible =>
            "Only failed, cancelled or stalled jobs can be requeued.",
        JobRequeueOutcome.Unsupported => "This job type cannot be requeued.",
        JobRequeueOutcome.SourceUnavailable => "The job's source no longer exists; it was marked as failed.",
        _ => "The job could not be requeued."
    };
}
