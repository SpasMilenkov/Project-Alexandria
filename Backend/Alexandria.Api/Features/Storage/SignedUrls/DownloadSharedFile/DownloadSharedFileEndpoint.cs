using Alexandria.Common.Exceptions.SignedUrls;
using Alexandria.Common.Services;
using Alexandria.Dto.SignedUrls;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.SignedUrls.DownloadSharedFile;

internal sealed class DownloadSharedFileRequest
{
    public string Token { get; init; } = string.Empty;
}

internal sealed class DownloadSharedFileEndpoint(ISignedUrlService signedUrlService)
    : Endpoint<DownloadSharedFileRequest, ShareDownloadResponse>
{
    public override void Configure()
    {
        Get("/share/{Token}/download");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DownloadSharedFileRequest req, CancellationToken ct)
    {
        try
        {
            var result = await signedUrlService.GetDownloadUrlAsync(req.Token, ct);
            await Send.OkAsync(result, ct);
        }
        catch (SignedUrlNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (SignedUrlExpiredException)
        {
            await Send.ErrorsAsync(429, ct);
        }
        catch (SignedUrlRevokedException)
        {
            await Send.ErrorsAsync(410, ct);
        }
    }
}