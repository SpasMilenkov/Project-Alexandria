using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Gallery.GetGalleryPhotos;

internal sealed class GetGalleryPhotosRequest
{
    public GallerySort Sort { get; init; } = GallerySort.Taken;

    /// <summary>The previous page's <c>nextCursor</c>, unchanged.</summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Start strictly before this date instead of at a cursor. For the taken sort it is a wall
    /// clock value, so send it with a Z suffix and no offset conversion.
    /// </summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>Inclusive lower bound, so a month section can load only itself.</summary>
    public DateTimeOffset? Since { get; init; }

    public int Limit { get; init; } = 100;
}

internal sealed class GetGalleryPhotosRequestValidator : Validator<GetGalleryPhotosRequest>
{
    private const int MaxLimit = 500;

    public GetGalleryPhotosRequestValidator()
    {
        RuleFor(x => x.Sort).IsInEnum();

        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit);

        RuleFor(x => x.Cursor)
            .Must((req, cursor) => GalleryCursorCodec.TryDecode(cursor, req.Sort, out _))
            .When(x => x.Cursor is not null)
            .WithMessage("Cursor is malformed or was issued for a different sort");

        RuleFor(x => x.Before)
            .Null()
            .When(x => x.Cursor is not null)
            .WithMessage("Pass either a cursor or a start date, not both");

        RuleFor(x => x.Since)
            .LessThan(x => x.Before)
            .When(x => x.Since.HasValue && x.Before.HasValue)
            .WithMessage("Since must be earlier than Before");
    }
}

/// <summary>
/// One keyset page of the caller's photo timeline, newest first.
/// </summary>
internal sealed class GetGalleryPhotosEndpoint(IGalleryService galleryService)
    : Endpoint<GetGalleryPhotosRequest, GalleryPageResponse>
{
    public override void Configure()
    {
        Get("/gallery/photos");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetGalleryPhotosRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var page = await galleryService.GetPageAsync(userId, new GalleryPageFilter
        {
            Sort = req.Sort,
            Cursor = req.Cursor,
            Before = req.Before?.UtcDateTime,
            Since = req.Since?.UtcDateTime,
            Limit = req.Limit,
        }, ct);

        await Send.OkAsync(page, ct);
    }
}