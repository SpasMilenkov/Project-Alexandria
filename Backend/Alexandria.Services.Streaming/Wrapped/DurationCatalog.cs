namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>Root object of the bundled duration catalog JSON.</summary>
internal sealed record DurationCatalog(IReadOnlyList<DurationAnchor> Anchors);
