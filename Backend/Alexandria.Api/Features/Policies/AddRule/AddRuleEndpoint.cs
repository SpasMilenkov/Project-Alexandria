using System.Text.Json;
using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Features.Policies.GetPolicy;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Policies;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Policies.AddRule;

internal sealed class AddRuleRequest
{
    public Guid PolicyId { get; init; }
    public PolicyActionType ActionType { get; init; }
    public PolicyTriggerType TriggerType { get; init; }
    public string TriggerValue { get; init; } = null!;
    public int Priority { get; init; }
    public bool ApplyOnNewVersion { get; init; }
    public JsonDocument Parameters { get; init; } = null!;

    public CreatePolicyRuleRequest ToServiceRequest() => new()
    {
        ActionType = ActionType,
        TriggerType = TriggerType,
        TriggerValue = TriggerValue,
        Priority = Priority,
        ApplyOnNewVersion = ApplyOnNewVersion,
        Parameters = Parameters,
    };
}

internal sealed class AddRuleValidator : Validator<AddRuleRequest>
{
    public AddRuleValidator()
    {
        RuleFor(x => x.PolicyId).NotEmpty();
        RuleFor(x => x.ActionType).IsInEnum();
        RuleFor(x => x.TriggerType).IsInEnum();
        RuleFor(x => x.TriggerValue).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Parameters).NotNull();
    }
}

internal sealed class AddRuleEndpoint(IDirectoryPolicyService policyService)
    : Endpoint<AddRuleRequest, PolicyRuleDto>
{
    public override void Configure()
    {
        Post("/policies/{PolicyId}/rules");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(AddRuleRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            var result = await policyService.AddRuleAsync(req.PolicyId, req.ToServiceRequest(), userId, ct);
            await Send.CreatedAtAsync<GetPolicyEndpoint>(
                new { DirectoryId = result.PolicyId },
                result,
                cancellation: ct);
        }
        catch (DirectoryPolicyNotFoundException)
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