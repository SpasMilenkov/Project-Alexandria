namespace Alexandria.Dto.Links;

/// <summary>Notes mentioning an item, most recently edited first.</summary>
public sealed record NoteBacklinksResponse(IReadOnlyList<NoteBacklinkDto> Notes);
