using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.MoveFiles;

sealed class MoveFilesRequest
{
    public Guid[] FileIds { get; set; }
    public Guid? DestinationId { get; set; }
}

sealed class MoveFilesEndpoint(IFileService fileService) : Endpoint<MoveFilesRequest>
{
    public override void Configure()
    {
        Post("files/move");
    }

    public override async Task HandleAsync(MoveFilesRequest r, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);

        await fileService.MoveFilesAsync(r.FileIds, r.DestinationId, userId, ct);

        await Send.OkAsync("File moved successfully", ct);
    }
}