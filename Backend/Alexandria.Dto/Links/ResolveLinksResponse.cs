namespace Alexandria.Dto.Links;

/// <summary>One entry per distinct requested target, in the order they were asked for.</summary>
public sealed record ResolveLinksResponse(IReadOnlyList<LinkTargetDto> Targets);
