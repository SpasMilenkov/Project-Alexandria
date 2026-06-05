using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Policies.DeletePolicy;

internal sealed class DeletePolicyRequest
{
    public Guid PolicyId { get; init; }
}

internal sealed class DeletePolicyValidator : Validator<DeletePolicyRequest>
{
    public DeletePolicyValidator()
    {
        RuleFor(x => x.PolicyId).NotEmpty();
    }
}

internal sealed class DeletePolicyEndpoint(IDirectoryPolicyService policyService)
    : Endpoint<DeletePolicyRequest>
{
    public override void Configure()
    {
        Delete("/policies/{PolicyId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeletePolicyRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await policyService.DeletePolicyAsync(req.PolicyId, userId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (DirectoryPolicyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}