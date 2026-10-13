using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Dto.Home;
using FastEndpoints;

namespace Alexandria.Api.Features.Home.GetWidgets;

public sealed class GetWidgetsEndpoint(IHomeWidgetService service) : EndpointWithoutRequest<HomeBoardDto>
{
    public override void Configure()
    {
        Get("/home/widgets");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await Send.OkAsync(await service.GetAsync(User.GetUserId(), ct), ct);
        }
        catch (HomeWidgetValidationException error)
        {
            foreach (var result in error.Errors)
            {
                foreach (var member in result.MemberNames)
                    AddError(member, result.ErrorMessage!);
            }

            await Send.ErrorsAsync(error.StatusCode, ct);
        }
    }
}
