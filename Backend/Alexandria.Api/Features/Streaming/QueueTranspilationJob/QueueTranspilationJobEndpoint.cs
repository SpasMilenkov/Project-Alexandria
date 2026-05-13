using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.QueueTranspilationJob;

sealed class QueueTranspilationJobRequest
{
    public Guid VersionId { get; set; }
}

sealed class QueueTranspilationJobEndpoint(ITranspilationJobService jobService) : Endpoint<QueueTranspilationJobRequest>
{
    public override void Configure()
    {
        Post("/streaming/job");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(QueueTranspilationJobRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await Send.OkAsync(await jobService.CreateJobAsync(req.VersionId, userId, ct), cancellation: ct);
    }
}