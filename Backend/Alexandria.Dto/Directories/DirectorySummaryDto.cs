using Alexandria.Dto.Files;

namespace Alexandria.Dto.Directories;

public record DirectorySummaryDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    UserDto OwnerUserDto);