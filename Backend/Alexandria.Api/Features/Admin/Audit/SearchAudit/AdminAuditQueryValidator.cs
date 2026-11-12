using Alexandria.Api.Features.Activity.Shared;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.Audit.SearchAudit;

sealed class AdminAuditQueryValidator : AuditFilterValidator<AdminAuditQuery>
{
    public AdminAuditQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, AuditQueryLimits.MaxPageSize);
        RuleFor(x => x.SortBy).IsInEnum();
        RuleFor(x => x.SortDirection).IsInEnum();
    }
}
