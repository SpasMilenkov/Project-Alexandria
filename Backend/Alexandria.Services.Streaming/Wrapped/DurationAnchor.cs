namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>One entry of the bundled duration catalog.</summary>
internal sealed record DurationAnchor(
    string Id,
    string Name,
    string Category,
    string Tier,
    double DurationMinutes,
    string DurationLabel,
    bool Exact,
    string Copy,
    double[]? DurationRangeMinutes);
