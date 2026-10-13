using Alexandria.Common.Settings.Values;
using FastEndpoints;

namespace Alexandria.Api.Features.Settings.Home.GetTimeZones;

public sealed class GetTimeZonesEndpoint : EndpointWithoutRequest<IReadOnlyList<string>>
{
    public override void Configure()
    {
        Get("/settings/home/time-zones");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HomeTimeZones.Identifiers.Count <= 2)
        {
            AddError("Server timezone data is unavailable. Install tzdata and ICU in the API runtime.");
            await Send.ErrorsAsync(503, ct);
            return;
        }

        await Send.OkAsync(HomeTimeZones.Identifiers, ct);
    }
}