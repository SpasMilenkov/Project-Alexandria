using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.SignedUrls;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.SignedUrls.GetShareLinksForFile;

internal sealed class GetShareLinksForFileRequest
{
    public Guid FileId { get; init; }
}

internal sealed class GetShareLinksForFileRequestValidator : Validator<GetShareLinksForFileRequest>
{
    public GetShareLinksForFileRequestValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("FileId is required.");
    }
}

internal sealed class GetShareLinksForFileEndpoint(ISignedUrlService signedUrlService)
    : Endpoint<GetShareLinksForFileRequest, IEnumerable<ShareLinkSummaryDto>>
{
    public override void Configure()
    {
        Get("/files/{FileId}/share");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetShareLinksForFileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var links = await signedUrlService.GetShareLinksForFileAsync(req.FileId, userId, ct);
        await Send.OkAsync(links, ct);
    }
}