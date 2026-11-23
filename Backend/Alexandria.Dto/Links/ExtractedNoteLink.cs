using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Links;

/// <summary>A distinct link target read from a note's text, with the line it first appears on.</summary>
public sealed record ExtractedNoteLink(LinkTargetKind Kind, Guid TargetId, string Snippet);
