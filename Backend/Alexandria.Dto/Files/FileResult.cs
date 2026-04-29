using Alexandria.Dto.Tags;

namespace Alexandria.Dto.Files;

public record FileResult(
    Guid FileId,
    string FileName,
    string MimeType,
    Guid? DirectoryId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt,
    FileVersionDto CurrentVersion,
    List<TagDto> Tags,
    UserDto Owner);