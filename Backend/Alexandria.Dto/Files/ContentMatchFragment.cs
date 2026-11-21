namespace Alexandria.Dto.Files;

/// <summary>
/// One excerpt around a match, as consecutive segments that join into the excerpt's text.
/// </summary>
public sealed record ContentMatchFragment(IReadOnlyList<ContentMatchSegment> Segments);