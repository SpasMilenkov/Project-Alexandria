using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Jobs;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Jobs.RequeueJob;

public class RequeueJobRequest
{
    public Guid JobId { get; set; }
}

public sealed record RequeueJobResponse(Guid JobId, JobRequeueOutcome Outcome);

sealed class RequeueJobEndpoint(
    IJobRequeueService requeueService,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork)
    : Endpoint<RequeueJobRequest, RequeueJobResponse>
{
    public override void Configure()
    {
        Post("/admin/jobs/{JobId}/requeue");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(RequeueJobRequest req, CancellationToken ct)
    {
        JobRequeueOutcome outcome;

        try
        {
            outcome = await requeueService.RequeueAsync(req.JobId, ct);
        }
        catch (MessageBrokerUnavailableException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(503, ct);
            return;
        }

        if (outcome == JobRequeueOutcome.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (outcome == JobRequeueOutcome.Requeued)
        {
            auditWriter.Record(new AuditEntry
            {
                EventCode = AuditEventCode.JobRequeued,
                OperationType = OperationType.Update,
                EntityType = EntityType.Job,
                EntityId = req.JobId,
            });
            await unitOfWork.SaveChangesAsync(ct);

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
