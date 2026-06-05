using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Policies;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Policies.GetPolicy;

internal sealed class GetPolicyRequest
{
    public Guid DirectoryId { get; init; }
}

internal sealed class GetPolicyValidator : Validator<GetPolicyRequest>
{
    public GetPolicyValidator()
    {
        RuleFor(x => x.DirectoryId).NotEmpty();
    }
}

internal sealed class GetPolicyEndpoint(IDirectoryPolicyService policyService)
    : Endpoint<GetPolicyRequest, DirectoryPolicyDto?>
{
    public override void Configure()
    {
        Get("/policies/by-directory/{DirectoryId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetPolicyRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await policyService.GetPolicyAsync(req.DirectoryId, userId, ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}