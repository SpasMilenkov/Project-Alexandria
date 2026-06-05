using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.UpdateTranspilationJob;

internal sealed class UpdateTranspilationJobRequest
{
    public Guid JobId { get; set; }
    public TranspilationStatus Status { get; set; }
    public AudioRung[]? AudioRungs { get; set; }
    public VideoRung[]? VideoRungs { get; set; }
}

internal sealed class UpdateTranspilationJobEndpoint(ITranspilationJobService jobService)
    : Endpoint<UpdateTranspilationJobRequest>
{
    public override void Configure()
    {
        Patch("streaming/update-transpilation-status");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UpdateTranspilationJobRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await jobService.UpdateStatusAsync(jobId: req.JobId,
            userId: userId,
            targetStatus: req.Status,
            audioRungs: req.AudioRungs,
            videoRungs: req.VideoRungs,
            ct: ct);

        await Send.NoContentAsync(cancellation: ct);
    }
}