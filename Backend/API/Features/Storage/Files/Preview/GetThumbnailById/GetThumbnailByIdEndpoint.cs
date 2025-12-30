using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.Preview.GetThumbnailById;

// public class GetThumbnailByIdEndpoint(IPreviewService previewService): Endpoint<GetThumbnailByIdRequest>
// {
//     public override void Configure()    
//     {
//         Get("/files/{id}/thumbnail/{width}/{height}");
//         AllowAnonymous();
//         Description(b => b.WithTags("Preview", "Files"));
//         Summary(s =>
//         {
//             s.Summary = "Endpoint for generating preview of files for use in the frontend";
//             s.Description = "Autodetects filetype and generates appropriate preview with predefined sizes. " +
//                             "If the preview exits it will be fetched from an already cached source," +
//                             " if there is no preview it will be generated on the fly which may consume larger amount of resources" +
//                             " and take a while."; 
//         });
//     }
//     public override async Task HandleAsync(GetThumbnailByIdRequest req, CancellationToken ct)
//     {
//         // var preview = await previewService.GetThumbnail(req.Id , req.Width, req.Height, ct);
//         //
//         // try
//         // {
//         //     if (preview is null) return;
//         //     HttpContext.Response.StatusCode = 200;
//         //     HttpContext.Response.ContentType = preview.Metadata.MimeType;
//         //     var encodedFileName = System.Net.WebUtility.UrlEncode(preview.Metadata.FileName)
//         //         .Replace("+", "%20");
//         //
//         //     HttpContext.Response.Headers.ContentDisposition = 
//         //         $"attachment; filename*=UTF-8''{encodedFileName}";
//         //     // Stream file from storage
//         //     await preview.FileStream.CopyToAsync(HttpContext.Response.Body, ct);
//         //     
//         // }
//         // catch (Exception e)
//         // {
//         //     Console.WriteLine(e);
//         //     throw;
//         // }
//         // finally
//         // {
//         //     if (preview is not null)
//         //         await preview.FileStream.DisposeAsync();
//         // }
//     }
// }