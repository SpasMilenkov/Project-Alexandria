using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Previews;

public sealed record UserPreviewDto(
    Guid PreviewId,
    Guid FileId,
    string FileName,
    PreviewKind Kind,
    long SizeBytes,
    DateTime CreatedAt);