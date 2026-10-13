using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Home;
using FastEndpoints;

namespace Alexandria.Api.Features.Home.ResetWidgets;

public sealed class ResetWidgetsEndpoint(IHomeWidgetService service) : EndpointWithoutRequest<HomeBoardDto>
{
    public override void Configure()
    {
        Delete("/home/widgets");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await service.ResetAsync(User.GetUserId(), ct), ct);
}
