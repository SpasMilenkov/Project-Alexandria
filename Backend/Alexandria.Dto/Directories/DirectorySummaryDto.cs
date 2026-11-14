using Alexandria.Dto.Files;

namespace Alexandria.Dto.Directories;

/// <param name="Size">
/// Bytes of the current versions of every live file in the folder and its subfolders.
/// </param>
public record DirectorySummaryDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    UserDto OwnerUserDto,
    bool IsStarred,
    long Size);