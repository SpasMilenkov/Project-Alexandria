using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Activity.GetCorrelatedActivity;

sealed class GetCorrelatedActivityRequestValidator : Validator<GetCorrelatedActivityRequest>
{
    public GetCorrelatedActivityRequestValidator()
    {
        RuleFor(x => x.CorrelationId).NotEmpty();
    }
}
