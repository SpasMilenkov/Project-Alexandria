using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Dto.Audit;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Activity.GetActivity;

sealed class AuditLogQueryValidator : Validator<AuditLogQuery>
{
    public AuditLogQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, AuditQueryLimits.MaxPageSize);
        RuleFor(x => x.SortBy).IsInEnum();
        RuleFor(x => x.SortDirection).IsInEnum();

        RuleFor(x => x.Before)
            .GreaterThanOrEqualTo(x => x.After)
            .When(x => x.Before.HasValue && x.After.HasValue)
            .WithMessage("Before must not be earlier than After.");
    }
}
