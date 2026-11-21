namespace Alexandria.Dto.Notes;

/// <summary>The name a rename settled on, extension included.</summary>
public sealed record NoteRenameResult(Guid FileId, string FileName);
