using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Values;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Settings.Home.UpdateHome;

public sealed class UpdateHomeRequest : HomeSettingsValue;

public sealed class UpdateHomeValidator : Validator<UpdateHomeRequest>
{
    public UpdateHomeValidator()
    {
        RuleFor(x => x).Custom((value, context) =>
        {
            foreach (var error in HomeSettingsValidation.Validate(value))
            foreach (var member in error.MemberNames)
                context.AddFailure(member, error.ErrorMessage!);
        });
    }
}

public sealed class UpdateHomeEndpoint(IUserSettingsService settingsService)
    : Endpoint<UpdateHomeRequest, HomeSettingsValue>
{
    public override void Configure()
    {
        Put("/settings/home");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UpdateHomeRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await settingsService.SetHomeAsync(userId, req, userId, ct);
        await Send.OkAsync(await settingsService.GetHomeAsync(userId, ct), ct);
    }
}