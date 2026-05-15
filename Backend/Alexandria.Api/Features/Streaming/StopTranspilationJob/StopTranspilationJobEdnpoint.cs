using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.StopTranspilationJob;

internal sealed class StopTranspilationJobRequest
{
    public Guid JobId { get; set; }
    public TranspilationStatus Status { get; set; }
}

internal sealed class StopTranspilationJobEndpoint(ITranspilationJobService jobService)
    : Endpoint<StopTranspilationJobRequest>
{
    public override void Configure()
    {
        Put("streaming/stop-transpilation");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(StopTranspilationJobRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await jobService.UpdateStatusAsync(jobId: req.JobId, userId: userId, status: req.Status, ct);

        await Send.NoContentAsync(cancellation: ct);
    }
}