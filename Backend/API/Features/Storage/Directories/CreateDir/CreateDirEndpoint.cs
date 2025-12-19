using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.CreateDir;

public class CreateDirEndpoint(IDirectoryService dirService): Endpoint<CreateDirRequest, CreateDirResult>
{

    public override void Configure()
    {
        Post("/directories");
        Summary(s =>
        {
            s.Summary = "Create a new directory";
            s.Description = "Creates a new directory for the current user. Directory names must be unique per parent-child directory relation.";
            s.Responses[200] = "Directory created successfully";
            s.Responses[400] = "Bad request - invalid directory name or duplicate";
            s.Responses[500] = "Internal server error";
            s.ExampleRequest = new CreateDirRequest
            {
                Name = "Important",
                ParentId = Guid.Empty
            };
        });
        Description(x => x.WithTags("Directories"));
    }

    public override async Task HandleAsync(CreateDirRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        // Also check if user is authenticated
        Console.WriteLine($"Is Authenticated: {User.Identity?.IsAuthenticated}");
        var userId = Guid.Parse(userIdString);
        var dir = await dirService.CreateDirectoryAsync(req.Name, userId, req.ParentId, ct);
        await Send.OkAsync( new CreateDirResult
        {
            Directory = dir
        } ,cancellation:ct);
    }
}