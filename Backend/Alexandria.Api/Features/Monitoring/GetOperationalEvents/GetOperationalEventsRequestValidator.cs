using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Monitoring.GetOperationalEvents;

public class GetOperationalEventsRequestValidator : Validator<GetOperationalEventsRequest>
{
    public GetOperationalEventsRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, ValidationConstants.PaginationConstants.MaxPageSize);

        RuleFor(x => x.ServiceType)
            .IsInEnum();

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.Severity)
            .IsInEnum();

        RuleFor(x => x.Code)
            .IsInEnum();

        RuleFor(x => x.To)
            .GreaterThan(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must be after 'From'.");
    }
}