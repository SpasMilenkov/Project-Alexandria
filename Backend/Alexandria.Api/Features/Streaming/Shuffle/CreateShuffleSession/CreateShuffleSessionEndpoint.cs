using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Shuffle;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.Shuffle.CreateShuffleSession;

internal sealed class CreateShuffleSessionRequest
{
    public Guid RequestId { get; set; }
    public PlaybackSourceDto Source { get; set; } = null!;
    public Guid? AnchorFileId { get; set; }
    public Guid? AnchorPlaylistItemId { get; set; }
    public Guid? AvoidFirstFileId { get; set; }
    public int Limit { get; set; } = 50;
}

internal sealed class CreateShuffleSessionRequestValidator : Validator<CreateShuffleSessionRequest>
{
    public CreateShuffleSessionRequestValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty();

        RuleFor(x => x.Source)
            .NotNull();

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 200);

        RuleFor(x => x.AnchorPlaylistItemId)
            .Null()
            .When(x => x.Source?.PlaylistId is null)
            .WithMessage("AnchorPlaylistItemId requires a playlist source.");

        RuleFor(x => x.AnchorFileId)
            .NotNull()
            .When(x => x.AnchorPlaylistItemId.HasValue)
            .WithMessage("AnchorPlaylistItemId requires AnchorFileId.");

        RuleFor(x => x.AvoidFirstFileId)
            .Null()
            .When(x => x.AnchorFileId.HasValue || x.AnchorPlaylistItemId.HasValue)
            .WithMessage("AvoidFirstFileId cannot be combined with an anchor.");

        RuleFor(x => x.AnchorFileId)
            .Null()
            .When(x => x.AvoidFirstFileId.HasValue)
            .WithMessage("Anchor cannot be combined with AvoidFirstFileId.");

        RuleFor(x => x.AnchorPlaylistItemId)
            .Null()
            .When(x => x.AvoidFirstFileId.HasValue)
            .WithMessage("Anchor cannot be combined with AvoidFirstFileId.");
    }
}

internal sealed class CreateShuffleSessionEndpoint(IShuffleService shuffleService)
    : Endpoint<CreateShuffleSessionRequest, ShuffleSessionResponse>
{
    public override void Configure()
    {
        Post("streaming/shuffle-sessions");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CreateShuffleSessionRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var userId = User.GetUserId();

        try
        {
            await Send.OkAsync(
                await shuffleService.CreateSessionAsync(userId, new CreateShuffleSessionCommand
                {
                    RequestId = req.RequestId,
                    Source = req.Source,
                    AnchorFileId = req.AnchorFileId,
                    AnchorPlaylistItemId = req.AnchorPlaylistItemId,
                    AvoidFirstFileId = req.AvoidFirstFileId,
                    Limit = req.Limit,
                }, ct),
                cancellation: ct);
        }
        catch (PlaylistNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (ShuffleRequestConflictException ex)
        {
            AddError(x => x.RequestId, ex.Message);
            await Send.ErrorsAsync(409, ct);
        }
        catch (ShuffleAnchorConflictException ex)
        {
            AddError(x => x.AnchorFileId, ex.Message);
            await Send.ErrorsAsync(409, ct);
        }
    }
}