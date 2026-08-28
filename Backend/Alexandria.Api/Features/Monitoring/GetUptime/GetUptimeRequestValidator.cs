using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Monitoring.GetUptime;

public class GetUptimeRequestValidator : Validator<GetUptimeRequest>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(366 * 5);

    public GetUptimeRequestValidator()
    {
        RuleFor(x => x.Service)
            .IsInEnum();

        RuleFor(x => x.From)
            .NotEmpty();

        RuleFor(x => x.To)
            .NotEmpty()
            .GreaterThan(x => x.From)
            .WithMessage("'To' must be after 'From'.")
            .Must((req, _) => req.To - req.From <= MaxRange)
            .WithMessage("Range must not exceed 5 years.");
    }
}