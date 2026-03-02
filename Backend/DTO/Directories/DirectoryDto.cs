using DTO.Files;

namespace DTO.Directories;

public record DirectoryDto(
    Guid Id,
    string Name,
    Guid ParentId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    UserDto OwnerUserDto,
    List<FileSummary> Files,
    List<DirectorySummaryDto> Directories
);