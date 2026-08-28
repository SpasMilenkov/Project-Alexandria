using Alexandria.Common.Services;
using Alexandria.Dto.TranspilationStats;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.TranspilationStats;

internal sealed class GetTranspilationTrendRequest
{
    /// <summary>Range start (inclusive, UTC). Defaults to 24h before <c>To</c>.</summary>
    public DateTime? From { get; set; }

    /// <summary>Range end (exclusive, UTC). Defaults to now.</summary>
    public DateTime? To { get; set; }

    /// <summary>Bucketing granularity: <c>Hour</c> or <c>Day</c>.</summary>
    public StatsBucket Bucket { get; set; } = StatsBucket.Hour;
}

sealed class GetTranspilationTrendRequestValidator : Validator<GetTranspilationTrendRequest>
{
    public GetTranspilationTrendRequestValidator()
    {
        RuleFor(x => x.Bucket)
            .IsInEnum();

        RuleFor(x => x.To)
            .GreaterThan(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must be after 'From'.");
    }
}

sealed class GetTranspilationTrendEndpoint(ITranspilationStatsService statsService)
    : Endpoint<GetTranspilationTrendRequest, TranspilationTrendResponse>
{
    public override void Configure()
    {
        Get("/admin/transpilation/trend");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(GetTranspilationTrendRequest req, CancellationToken ct)
    {
        var to = (req.To ?? DateTime.UtcNow).ToUniversalTime();
        var from = (req.From ?? to.AddHours(-24)).ToUniversalTime();

        var result = await statsService.GetTrendAsync(from, to, req.Bucket, ct);
        await Send.OkAsync(result, ct);
    }
}