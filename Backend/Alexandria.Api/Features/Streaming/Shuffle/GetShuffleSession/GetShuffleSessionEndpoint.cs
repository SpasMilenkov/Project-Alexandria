using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Shuffle;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.Shuffle.GetShuffleSession;

internal sealed class GetShuffleSessionRequest
{
    public Guid Id { get; set; }
    public int Offset { get; set; } = 0;
    public int Limit { get; set; } = 50;
}

internal sealed class GetShuffleSessionRequestValidator : Validator<GetShuffleSessionRequest>
{
    public GetShuffleSessionRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 200);
    }
}

internal sealed class GetShuffleSessionEndpoint(IShuffleService shuffleService)
    : Endpoint<GetShuffleSessionRequest, ShuffleSessionResponse>
{
    public override void Configure()
    {
        Get("streaming/shuffle-sessions/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetShuffleSessionRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var userId = User.GetUserId();

        try
        {
            await Send.OkAsync(
                await shuffleService.GetSessionBatchAsync(userId, req.Id, req.Offset, req.Limit, ct),
                cancellation: ct);
        }
        catch (ShuffleSessionNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (ShuffleOffsetOutOfRangeException ex)
        {
            AddError(x => x.Offset, ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}