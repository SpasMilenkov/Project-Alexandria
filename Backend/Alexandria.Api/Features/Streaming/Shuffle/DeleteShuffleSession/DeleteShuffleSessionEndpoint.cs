using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.Shuffle.DeleteShuffleSession;

internal sealed class DeleteShuffleSessionRequest
{
    public Guid Id { get; set; }
}

internal sealed class DeleteShuffleSessionRequestValidator : Validator<DeleteShuffleSessionRequest>
{
    public DeleteShuffleSessionRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

internal sealed class DeleteShuffleSessionEndpoint(IShuffleService shuffleService)
    : Endpoint<DeleteShuffleSessionRequest>
{
    public override void Configure()
    {
        Delete("streaming/shuffle-sessions/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeleteShuffleSessionRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var userId = User.GetUserId();

        try
        {
            await shuffleService.DeleteSessionAsync(userId, req.Id, ct);
            await Send.NoContentAsync(ct);
        }
        catch (ShuffleSessionNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}