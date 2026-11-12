using Alexandria.Api.Features.Activity.Shared;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Activity.GetEntityHistory;

sealed class GetEntityHistoryRequestValidator : Validator<GetEntityHistoryRequest>
{
    public GetEntityHistoryRequestValidator()
    {
        RuleFor(x => x.EntityType).IsInEnum();
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, AuditQueryLimits.MaxPageSize);
    }
}
