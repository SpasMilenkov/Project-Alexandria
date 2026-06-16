using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Features.Storage.SignedUrls.GetSharedFile;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Services;
using Alexandria.Dto.SignedUrls;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.SignedUrls.CreateShareLink;

internal sealed class CreateShareLinkRequest
{
    public Guid FileId { get; init; }

    /// <summary>
    /// Link lifetime. Must be between 1 hour and 30 days. Defaults to 7 days if omitted.
    /// </summary>
    public TimeSpan? Expiry { get; init; }

    /// <summary>
    /// When set, the link permanently resolves to this version.
    /// Omit to always follow the file's current version.
    /// </summary>
    public Guid? FileVersionId { get; init; }

    public int? MaxAccessCount { get; init; }
}

internal sealed class CreateShareLinkRequestValidator : Validator<CreateShareLinkRequest>
{
    private static readonly TimeSpan MinExpiry = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxExpiry = TimeSpan.FromDays(30);

    public CreateShareLinkRequestValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("FileId is required.");

        RuleFor(x => x.Expiry)
            .Must(e => e is null || (e >= MinExpiry && e <= MaxExpiry))
            .WithMessage("Expiry must be between 1 hour and 30 days.");

        RuleFor(x => x.FileVersionId)
            .NotEmpty()
            .When(x => x.FileVersionId.HasValue)
            .WithMessage("FileVersionId must not be an empty guid.");

        RuleFor(x => x.MaxAccessCount)
            .GreaterThan(0)
            .WithMessage("MaxAccessCount must be greater than zero.");
    }
}

internal sealed class CreateShareLinkEndpoint(ISignedUrlService signedUrlService)
    : Endpoint<CreateShareLinkRequest, CreateShareLinkResponse>
{
    public override void Configure()
    {
        Post("/files/{FileId}/share");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CreateShareLinkRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            var result = await signedUrlService.CreateShareLinkAsync(
                req.FileId,
                userId,
                req.Expiry,
                req.FileVersionId,
                req.MaxAccessCount,
                ct);

            await Send.CreatedAtAsync<GetSharedFileEndpoint>(
                new { Token = result.Token },
                result,
                cancellation: ct);
        }
        catch (FileVersionNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}