using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.CopyFiles;

sealed class CopyFilesRequest
{
    public Guid[] FileIds { get; set; }
    public Guid DestinationId { get; set; }
}

sealed class CopyDirectoryEndpoint(IFileService fileService) : Endpoint<CopyFilesRequest>
{
    public override void Configure()
    {
        Post("/files/copy");
    }

    public override async Task HandleAsync(CopyFilesRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);

        await fileService.CopyFilesAsync(req.FileIds, req.DestinationId, userId, ct);
        await Send.OkAsync(cancellation: ct);
    }
}