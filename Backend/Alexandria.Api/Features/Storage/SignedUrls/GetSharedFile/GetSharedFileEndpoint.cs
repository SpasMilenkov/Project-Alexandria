using Alexandria.Common.Exceptions.SignedUrls;
using Alexandria.Common.Services;
using Alexandria.Dto.SignedUrls;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.SignedUrls.GetSharedFile;

internal sealed class GetSharedFileRequest
{
    public string Token { get; init; } = string.Empty;
}

internal sealed class GetSharedFileEndpoint(ISignedUrlService signedUrlService)
    : Endpoint<GetSharedFileRequest, SharedFileMetadataDto>
{
    public override void Configure()
    {
        Get("/share/{Token}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetSharedFileRequest req, CancellationToken ct)
    {
        try
        {
            var result = await signedUrlService.GetSharedFileMetadataAsync(req.Token, ct);
            await Send.OkAsync(result, ct);
        }
        catch (SignedUrlNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (SignedUrlExpiredException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}