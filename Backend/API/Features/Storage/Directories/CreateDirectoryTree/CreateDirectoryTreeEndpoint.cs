using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.CreateDirectoryTree;

public sealed class CreateDirectoryTreeRequest
{
    public Guid? ParentId { get; set; }
    public List<string> Paths { get; set; } = [];
}

sealed class CreateDirectoryTreeEndpoint(IDirectoryService directoryService)
    : Endpoint<CreateDirectoryTreeRequest, Dictionary<string, Guid?>>
{
    public override void Configure()
    {
        Post("directories/create-subtree");
    }

    public override async Task HandleAsync(CreateDirectoryTreeRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(
            await directoryService.CreateDirectorySubTreeAsync(
                paths: req.Paths,
                parentId: req.ParentId,
                userId: userId,
                ct: ct),
            ct);
    }
}