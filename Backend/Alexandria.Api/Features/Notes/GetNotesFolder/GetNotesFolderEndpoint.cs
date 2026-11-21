using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Directories;
using FastEndpoints;

namespace Alexandria.Api.Features.Notes.GetNotesFolder;

/// <summary>
/// The caller's Notes folder. 204 until the first note creates it, since not having one yet is a
/// normal state rather than a missing resource.
/// </summary>
internal sealed class GetNotesFolderEndpoint(INoteService noteService)
    : EndpointWithoutRequest<DirectorySummaryDto>
{
    public override void Configure()
    {
        Get("/notes/folder");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var folder = await noteService.GetNotesFolderAsync(User.GetUserId(), ct);

        if (folder is null)
        {
            await Send.NoContentAsync(ct);
            return;
        }

        await Send.OkAsync(folder, ct);
    }
}
