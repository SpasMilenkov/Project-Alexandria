using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.QueueTranspilationJob;

internal sealed class QueueTranspilationJobRequest
{
    public Guid VersionId { get; set; }
    public AudioRung[] AudioRungs { get; set; } = [];
    public VideoRung[] VideoRungs { get; set; } = [];
}

internal sealed class QueueTranspilationJobEndpoint(ITranspilationJobService jobService)
    : Endpoint<QueueTranspilationJobRequest>
{
    public override void Configure()
    {
        Post("/streaming/job");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(QueueTranspilationJobRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await Send.OkAsync(await jobService.CreateJobAsync(req.VersionId, userId, req.AudioRungs, req.VideoRungs, ct),
            cancellation: ct);
    }
}