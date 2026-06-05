using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Features.Streaming.GetFileSessions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.StartSession;

internal sealed class StartSessionEndpointRequest
{
    public Guid FileId { get; init; }
    public long StartPositionSeconds { get; init; }
}

internal sealed class StartSessionValidator : Validator<StartSessionEndpointRequest>
{
    public StartSessionValidator()
    {
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.StartPositionSeconds).GreaterThanOrEqualTo(0);
    }
}

internal sealed class StartSessionEndpoint(IStreamHistoryService historyService)
    : Endpoint<StartSessionEndpointRequest, StreamSessionDto>
{
    public override void Configure()
    {
        Post("/stream-history/sessions");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(StartSessionEndpointRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var session = await historyService.StartSessionAsync(new StartSessionRequest
        {
            FileId = req.FileId,
            StartPositionSeconds = req.StartPositionSeconds,
        }, userId, ct);

        await Send.CreatedAtAsync<GetFileSessionsEndpoint>(
            new { StreamHistoryId = session.StreamHistoryId },
            session,
            cancellation: ct);
    }
}