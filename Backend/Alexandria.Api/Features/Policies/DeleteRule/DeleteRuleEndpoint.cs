using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Policies.DeleteRule;

internal sealed class DeleteRuleRequest
{
    public Guid RuleId { get; init; }
}

internal sealed class DeleteRuleValidator : Validator<DeleteRuleRequest>
{
    public DeleteRuleValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
    }
}

internal sealed class DeleteRuleEndpoint(IDirectoryPolicyService policyService)
    : Endpoint<DeleteRuleRequest>
{
    public override void Configure()
    {
        Delete("/policies/rules/{RuleId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeleteRuleRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await policyService.DeleteRuleAsync(req.RuleId, userId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (PolicyRuleNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}