using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;

namespace Alexandria.Dto.Directories;

/// <param name="Size">
/// Bytes of the current versions of every live file in the folder and its subfolders.
/// </param>
/// <param name="SystemKind">Set on the owner's Notes folder, which can't be trashed, moved or renamed.</param>
public record DirectorySummaryDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    UserDto OwnerUserDto,
    bool IsStarred,
    long Size,
    DirectorySystemKind? SystemKind = null);