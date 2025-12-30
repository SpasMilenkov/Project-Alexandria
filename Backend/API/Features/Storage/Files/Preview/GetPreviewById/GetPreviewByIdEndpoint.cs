using System.Security.Claims;
using Common.Services;
using DTO.Files;
using FastEndpoints;

namespace API.Features.Storage.Files.Preview.GetPreviewById;

public class GetPreviewByIdEndpoint(IPreviewService previewService) : Endpoint<GetPreviewByIdRequest, PreviewResultDto>
{
    public override void Configure()
    {
        Get("/files/{id}/preview");
        Description(b => b.WithTags("Preview", "Files"));
        Summary(s =>
        {
            s.Summary = "Endpoint for generating preview of files for use in the frontend";
            s.Description = "Autodetects filetype and generates appropriate preview with predefined sizes. " +
                            "If the preview exits it will be fetched from an already cached source," +
                            " if there is no preview it will be generated on the fly which may consume larger amount of resources" +
                            " and take a while."; 
        });
        
    }
    
    public override async Task HandleAsync(GetPreviewByIdRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);
        
        var preview = await previewService.GetPreviewUrl(req.Id, userId, ct);
    
        try
        {
            if (preview is null)
            {
                return;
            }
            // Stream file from storage
            await Send.OkAsync(preview, ct);

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}