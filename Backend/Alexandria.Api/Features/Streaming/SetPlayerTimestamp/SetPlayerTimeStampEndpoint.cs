using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.SetPlayerTimestamp;

internal sealed class SetPlayerTimestampRequest
{
    public Guid FileId { get; set; }
    public long PositionSeconds { get; set; }
    public bool Completed { get; set; }
}

internal sealed class SetPlayerTimestampEndpoint(IStreamHistoryService streamHistoryService)
    : Endpoint<SetPlayerTimestampRequest>
{
    public override void Configure()
    {
        Post("/streaming/set-timestamp");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(SetPlayerTimestampRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await streamHistoryService.UpsertPositionAsync(userId: userId,
            fileId: req.FileId,
            positionSeconds: req.PositionSeconds,
            completed: req.Completed,
            ct: ct);

        await Send.NoContentAsync(cancellation: ct);
    }
}