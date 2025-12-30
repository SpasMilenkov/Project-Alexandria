using DTO.Tags;
using File = Models.File;

namespace DTO.Files;

public record FileResult(
    Guid FileId,
    string FileName,
    string MimeType,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt,
    FileVersionDto CurrentVersion,
    List<TagDto> Tags,
    UserDto Owner);