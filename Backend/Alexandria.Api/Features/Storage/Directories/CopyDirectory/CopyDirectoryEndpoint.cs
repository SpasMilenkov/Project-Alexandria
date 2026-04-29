using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Directories.CopyDirectory;

public sealed class CopyDirectoryRequest
{
    public Guid DirectoryId { get; set; }
    public Guid? DestinationId { get; set; }
}

sealed class CopyDirectoryEndpoint(IDirectoryService directoryService) : Endpoint<CopyDirectoryRequest>
{
    public override void Configure()
    {
        Post("/directories/copy");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CopyDirectoryRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await directoryService.CopyDirectoryAsync(req.DirectoryId, req.DestinationId, userId, ct);
        await Send.OkAsync(cancellation: ct);
    }
}