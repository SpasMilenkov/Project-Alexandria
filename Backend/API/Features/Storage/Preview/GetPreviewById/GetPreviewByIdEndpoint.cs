using Common.Services;
using FastEndpoints;
using SixLabors.ImageSharp.Diagnostics;

namespace API.Features.Storage.Preview.GetPreviewById;

public class GetPreviewByIdEndpoint(IPreviewService previewService) : Endpoint<GetPreviewByIdRequest>
{
    public override void Configure()
    {
        Get("/files/{id}/preview");
        AllowAnonymous();
        Description(b => b.WithTags("Preview"));
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
        var preview = await previewService.GetPreview(req.Id, ct);

        try
        {

            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = preview.Metadata.MimeType;
            var encodedFileName = System.Net.WebUtility.UrlEncode(preview.Metadata.FileName)
                .Replace("+", "%20");
        
            HttpContext.Response.Headers.ContentDisposition = 
                $"attachment; filename*=UTF-8''{encodedFileName}";
            // Stream file from storage
            await preview.FileStream.CopyToAsync(HttpContext.Response.Body, ct);
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            await preview.FileStream.DisposeAsync();
        }
    }
}