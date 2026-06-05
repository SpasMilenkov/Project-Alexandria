using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Features.Policies.GetPolicy;
using Alexandria.Common.Services;
using Alexandria.Dto.Policies;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Policies.CreatePolicy;

internal sealed class CreatePolicyValidator : Validator<CreateDirectoryPolicyRequest>
{
    public CreatePolicyValidator()
    {
        RuleFor(x => x.DirectoryId).NotEmpty();
    }
}

internal sealed class CreatePolicyEndpoint(IDirectoryPolicyService policyService)
    : Endpoint<CreateDirectoryPolicyRequest, DirectoryPolicyDto>
{
    public override void Configure()
    {
        Post("/policies");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CreateDirectoryPolicyRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            var result = await policyService.CreatePolicyAsync(req, userId, ct);
            await Send.CreatedAtAsync<GetPolicyEndpoint>(
                new { DirectoryId = result.DirectoryId },
                result,
                cancellation: ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(409, ct);
        }
    }
}