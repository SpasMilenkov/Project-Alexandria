using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.Preview.GetThumbnailById;

public class GetThumbnailByIdEndpoint(IPreviewService previewService) : Endpoint<GetThumbnailByIdRequest>
{
    public override void Configure()
    {
        Get("/files/{id}/thumbnail/{width}/{height}");
        Description(b => b.WithTags("Preview", "Files"));
        Summary(s =>
        {
            s.Summary = "Endpoint for generating preview of files for use in the frontend";
            s.Description = "Autodetects filetype and generates appropriate preview with predefined sizes. " +
                            "If the preview exits it will be fetched from an already cached source," +
                            " if there is no preview it will be generated on the fly which may consume larger amount of resources" +
                            " and take a while.";
        });
        ResponseCache(120);

    }
    public override async Task HandleAsync(GetThumbnailByIdRequest req, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
