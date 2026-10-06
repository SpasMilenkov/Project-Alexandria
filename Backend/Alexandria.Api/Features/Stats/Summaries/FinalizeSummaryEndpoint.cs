using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.OverviewSummaries;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Stats.Summaries;

internal sealed class FinalizeSummaryRequest
{
    public SummaryKind Kind { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

internal sealed class FinalizeSummaryValidator : Validator<FinalizeSummaryRequest>
{
    public FinalizeSummaryValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.From).NotNull().WithMessage("From is required to finalize a period");
        RuleFor(x => x.To).NotNull().WithMessage("To is required to finalize a period");

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.To.Value > x.From.Value)
            .WithMessage("To must be after From");
    }
}

internal sealed class FinalizeSummaryEndpoint(IOverviewSummaryService summaries)
    : Endpoint<FinalizeSummaryRequest, OverviewSummaryDto>
{
    public override void Configure()
    {
        Post("/stats/summaries/{Kind}/finalize");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(FinalizeSummaryRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var period = new Common.Summaries.SummaryPeriod(
            req.From!.Value.UtcDateTime, req.To!.Value.UtcDateTime);

        try
        {
            await Send.OkAsync(await summaries.FinalizeAsync(userId, req.Kind, period, ct), ct);
        }
        catch (SummaryPeriodNotClosedException ex)
        {
            AddError(x => x.To, ex.Message);
            await Send.ErrorsAsync(422, ct);
        }
    }
}
