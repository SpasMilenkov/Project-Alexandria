using System.Security.Claims;
using Common;
using Common.Services;
using FastEndpoints;

namespace API.Features.Tags.AddTagsToFile;




public class AddTagsToFileEndpoint(IFileTagService tagService) : Endpoint<AddTagsToFileRequest, AddTagsToFileResponse>
{
    public override void Configure()
    {
        Post("/files/{FileId}/tags");
        Summary(s =>
        {
            s.Summary = "Add tags to a file";
            s.Description = "Associates multiple tags with a file. Only tags owned by the user will be added.";
            s.Responses[200] = "Tags added successfully";
            s.Responses[400] = "Bad request - invalid data";
            s.Responses[404] = "File not found";
            s.ExampleRequest = new AddTagsToFileRequest 
            { 
                FileId = Guid.NewGuid(), 
                TagIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() } 
            };
        });
    }

    public override async Task HandleAsync(AddTagsToFileRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);

        try
        {
            await tagService.AddTagsToFileAsync(req.FileId, req.TagIds, userId, ct);
            
            await Send.OkAsync(new AddTagsToFileResponse
            {
                FileId = req.FileId,
                TagsAdded = req.TagIds.Count,
                Message = $"Successfully added {req.TagIds.Count} tag(s) to file"
            }, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }
}