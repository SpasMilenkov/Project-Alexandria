using System.Text.Json;
using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Policies;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Policies.UpdateRule;

internal sealed class UpdateRuleRequest
{
    public Guid RuleId { get; init; }
    public PolicyTriggerType TriggerType { get; init; }
    public string TriggerValue { get; init; } = null!;
    public int Priority { get; init; }
    public bool ApplyOnNewVersion { get; init; }
    public JsonDocument Parameters { get; init; } = null!;

    public UpdatePolicyRuleRequest ToServiceRequest() => new()
    {
        TriggerType = TriggerType,
        TriggerValue = TriggerValue,
        Priority = Priority,
        ApplyOnNewVersion = ApplyOnNewVersion,
        Parameters = Parameters,
    };
}

internal sealed class UpdateRuleValidator : Validator<UpdateRuleRequest>
{
    public UpdateRuleValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.TriggerType).IsInEnum();
        RuleFor(x => x.TriggerValue).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Parameters).NotNull();
    }
}

internal sealed class UpdateRuleEndpoint(IDirectoryPolicyService policyService)
    : Endpoint<UpdateRuleRequest, PolicyRuleDto>
{
    public override void Configure()
    {
        Patch("/policies/rules/{RuleId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UpdateRuleRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            var result = await policyService.UpdateRuleAsync(req.RuleId, req.ToServiceRequest(), userId, ct);
            await Send.OkAsync(result, ct);
        }
        catch (PolicyRuleNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidPolicyParametersException ex)
        {
            AddError(nameof(req.Parameters), ex.Message);
            await Send.ErrorsAsync(422, ct);
        }
    }
}