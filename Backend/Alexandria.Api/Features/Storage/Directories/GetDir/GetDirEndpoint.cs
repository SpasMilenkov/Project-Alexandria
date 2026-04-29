using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Directories.GetDir;

public class GetDirEndpoint(IDirectoryService dirService) : Endpoint<GetDirRequest, GetDirResult>
{
    public override void Configure()
    {
        Get("/directories");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetDirRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var dir = await dirService.GetDirectoryDtoByIdAsync(req.DirectoryId, userId, ct);
        await Send.OkAsync(new GetDirResult
        {
            Directory = dir
        }, cancellation: ct);
    }
}