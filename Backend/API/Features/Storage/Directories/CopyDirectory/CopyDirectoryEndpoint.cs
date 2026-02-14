using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.CopyDirectory;

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
        AllowAnonymous();
    }

    public override async Task HandleAsync(CopyDirectoryRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);

        await directoryService.CopyDirectoryAsync(req.DirectoryId, req.DestinationId, userId, ct);
        await Send.OkAsync(cancellation: ct);
    }
}