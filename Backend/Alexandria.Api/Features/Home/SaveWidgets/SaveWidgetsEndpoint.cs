using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Common.Widgets;
using Alexandria.Dto.Home;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Home.SaveWidgets;

public sealed class SaveWidgetsValidator : Validator<HomeBoardChanges>
{
    public SaveWidgetsValidator()
    {
        RuleFor(value => value).Custom((value, context) =>
        {
            try
            {
                HomeWidgetBatchValidation.ValidateRequest(value);
            }
            catch (HomeWidgetValidationException error)
            {
                foreach (var result in error.Errors)
                {
                    foreach (var member in result.MemberNames)
                        context.AddFailure(member, result.ErrorMessage!);
                }
            }
        });
    }
}

public sealed class SaveWidgetsEndpoint(IHomeWidgetService service) : Endpoint<HomeBoardChanges, HomeBoardDto>
{
    public override void Configure()
    {
        Patch("/home/widgets");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(HomeBoardChanges req, CancellationToken ct)
    {
        try
        {
            await Send.OkAsync(await service.SaveAsync(User.GetUserId(), req, ct), ct);
        }
        catch (HomeWidgetNotFoundException)
        {
            await Send.NotFoundAsync(ct);
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
