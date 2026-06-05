using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.CloseSession;

internal sealed class CloseSessionEndpointRequest
{
    public Guid SessionId { get; init; }
    public long EndPositionSeconds { get; init; }
    public long ListenedSeconds { get; init; }
}

internal sealed class CloseSessionValidator : Validator<CloseSessionEndpointRequest>
{
    public CloseSessionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.EndPositionSeconds).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ListenedSeconds).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CloseSessionEndpoint(IStreamHistoryService historyService)
    : Endpoint<CloseSessionEndpointRequest, StreamHistoryDto>
{
    public override void Configure()
    {
        Patch("/stream-history/sessions/{SessionId}/close");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CloseSessionEndpointRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await historyService.CloseSessionAsync(req.SessionId, new CloseSessionRequest
        {
            EndPositionSeconds = req.EndPositionSeconds,
            ListenedSeconds = req.ListenedSeconds,
        }, userId, ct);

        await Send.OkAsync(result, ct);
    }
}