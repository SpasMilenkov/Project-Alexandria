using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetMediaTimeStamp;

internal sealed class GetMediaTimeStampRequest
{
}

internal sealed class GetMediaTimeStampResponse
{
}

internal sealed class GetMediaTimeStampEndpoint : Endpoint<GetMediaTimeStampRequest, GetMediaTimeStampResponse>
{
    public override void Configure()
    {
        Get("/streaming/timestamp");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetMediaTimeStampRequest req, CancellationToken ct)
    {
    }
}