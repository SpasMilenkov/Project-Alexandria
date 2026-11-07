using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.Preview.RemoveVersionCover;

internal sealed class RemoveVersionCoverRequest
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
}

internal sealed class RemoveVersionCoverEndpoint(IPreviewArtifactService previewArtifacts)
    : Endpoint<RemoveVersionCoverRequest>
{
    public override void Configure()
    {
        Delete("/files/{id}/versions/{versionId}/cover");
        Description(b => b.WithTags("Preview", "Files"));
        Summary(s =>
        {
            s.Summary = "Removes a version's custom cover";
            s.Description = "The automatic thumbnail regenerates on the next read.";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(RemoveVersionCoverRequest req, CancellationToken ct)
    {
        try
        {
            await previewArtifacts.RemoveCustomCoverAsync(req.Id, req.VersionId, User.GetUserId(), ct);
            await Send.NoContentAsync(ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
