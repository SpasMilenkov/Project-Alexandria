using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Services;
using Alexandria.Dto.Policies;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Policies.UpdatePolicy;

internal sealed class UpdatePolicyRequest
{
    public Guid PolicyId { get; init; }
    public bool InheritedByChildren { get; init; }
}

internal sealed class UpdatePolicyValidator : Validator<UpdatePolicyRequest>
{
    public UpdatePolicyValidator()
    {
        RuleFor(x => x.PolicyId).NotEmpty();
    }
}

internal sealed class UpdatePolicyEndpoint(IDirectoryPolicyService policyService)
    : Endpoint<UpdatePolicyRequest, DirectoryPolicyDto>
{
    public override void Configure()
    {
        Patch("/policies/{PolicyId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UpdatePolicyRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            var result = await policyService.UpdatePolicyAsync(req.PolicyId, req.InheritedByChildren, userId, ct);
            await Send.OkAsync(result, ct);
        }
        catch (DirectoryPolicyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}