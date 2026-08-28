using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Monitoring.ReportProblem;

public class ReportProblemRequestValidator : Validator<ReportProblemRequest>
{
    public ReportProblemRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(ValidationConstants.StringLengths.ExtraLongString);

        RuleFor(x => x.PageContext)
            .MaximumLength(ValidationConstants.StringLengths.MediumString);
    }
}