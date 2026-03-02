using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.GetDirectoryPath;

public class GetDirectoryPathEndpoint(IDirectoryService dirService) : Endpoint<GetDirectoryPathRequest, GetDirectoryPathResponse>
{
    public override void Configure()
    {
        Get("directories/path/{id}");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetDirectoryPathRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var pathParts = await dirService.GetDirectoryPathAsync(req.Id, userId, ct);

        await Send.OkAsync(new GetDirectoryPathResponse
        {
            PathParts = pathParts
        }, ct);
    }
}