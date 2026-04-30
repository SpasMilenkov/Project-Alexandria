using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.Preview.GetPreviewById;

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
        ResponseCache(60);
    }

    public override async Task HandleAsync(GetPreviewByIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var preview = await previewService.GetPreviewUrlAsync(req.Id, userId, ct);

        try
        {
            if (preview is null) return;

            await Send.OkAsync(preview, ct);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}