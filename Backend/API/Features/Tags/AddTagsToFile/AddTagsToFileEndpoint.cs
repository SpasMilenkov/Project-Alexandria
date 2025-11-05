using Common;
using Common.Services;
using FastEndpoints;

namespace API.Features.Tags.AddTagsToFile;




public class AddTagsToFileEndpoint(IFileTagService tagService) : Endpoint<AddTagsToFileRequest, AddTagsToFileResponse>
{
    public override void Configure()
    {
        Post("/files/{FileId}/tags");
        AllowAnonymous(); // TODO: Replace with proper authorization
        
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
        // TODO: Extract from JWT token
        var userId = Guid.Parse("019a3f05-659b-7628-9102-1eef78035977"); // Placeholder

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