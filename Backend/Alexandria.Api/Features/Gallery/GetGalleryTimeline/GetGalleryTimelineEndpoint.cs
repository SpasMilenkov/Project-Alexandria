using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Gallery.GetGalleryTimeline;

internal sealed class GetGalleryTimelineRequest
{
    public GallerySort Sort { get; init; } = GallerySort.Taken;

    /// <summary>The viewer's IANA zone, used to put upload times into the viewer's months.</summary>
    public string TimeZone { get; init; } = "UTC";
}

internal sealed class GetGalleryTimelineRequestValidator : Validator<GetGalleryTimelineRequest>
{
    public GetGalleryTimelineRequestValidator()
    {
        RuleFor(x => x.Sort).IsInEnum();

        // .NET on Linux also accepts Windows zone ids, which Postgres would reject at query time.
        RuleFor(x => x.TimeZone)
            .NotEmpty()
            .Must(zone => TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)
                          && TimeZoneInfo.TryConvertIanaIdToWindowsId(zone, out _))
            .WithMessage("TimeZone must be an IANA zone such as Europe/Sofia");
    }
}

/// <summary>
/// Photo counts per month for the caller's library, which drive the scrubber and section sizes.
/// </summary>
internal sealed class GetGalleryTimelineEndpoint(IGalleryService galleryService)
    : Endpoint<GetGalleryTimelineRequest, GalleryTimelineResponse>
{
    public override void Configure()
    {
        Get("/gallery/timeline");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetGalleryTimelineRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var timeline = await galleryService.GetTimelineAsync(userId, req.Sort, req.TimeZone, ct);

        await Send.OkAsync(timeline, ct);
    }
}