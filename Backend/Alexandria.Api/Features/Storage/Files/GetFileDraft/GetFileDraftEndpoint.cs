using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.GetFileDraft;

internal sealed class GetFileDraftRequest
{
    public Guid Id { get; set; }
}

/// <summary>
/// The editor's draft of a file, read when the editor opens it. 204 when there is none, which is
/// the normal state between editing sessions.
/// </summary>
internal sealed class GetFileDraftEndpoint(IFileDraftService drafts)
    : Endpoint<GetFileDraftRequest, FileDraftDto>
{
    // The browser cache is shared by every account signed in on it.
    private const string NoStore = "no-store";

    public override void Configure()
    {
        Get("/files/{id}/draft");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetFileDraftRequest req, CancellationToken ct)
    {
        FileDraftDto? draft;

        try
        {
            draft = await drafts.GetAsync(req.Id, User.GetUserId(), ct);
        }
        catch (FileRecordNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = NoStore;

        if (draft is null)
        {
            await Send.NoContentAsync(ct);
            return;
        }

        await Send.OkAsync(draft, ct);
    }
}
