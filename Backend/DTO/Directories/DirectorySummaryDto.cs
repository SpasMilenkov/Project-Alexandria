using DTO.Files;

namespace DTO.Directories;

public record DirectorySummaryDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    UserDto OwnerUserDto);