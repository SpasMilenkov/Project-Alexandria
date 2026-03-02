using DTO.Directories;
using DTO.Files;
using Models;
using Directory = Models.Directory;
using File = Models.File;

namespace Storage.Directories;

public static class DirectoryMappings
{
    public static DirectoryDto ToDto(this Directory dir)
    {
        ArgumentNullException.ThrowIfNull(dir);

        return new DirectoryDto(
            Id: dir.Id,
            Name: dir.Name,
            ParentId: dir.ParentId ?? Guid.Empty,
            CreatedAt: dir.CreatedAt,
            UpdatedAt: dir.UpdatedAt ?? dir.CreatedAt,
            OwnerUserDto: dir.Owner?.ToUserDto() ?? new UserDto(),
            Files: dir.Files?.Select(f => f.ToFileSummary()).ToList() ?? [],
            Directories: dir.Children?.Select(c => c.ToSummaryDto()).ToList() ?? []
        );
    }

    public static DirectorySummaryDto ToSummaryDto(this Directory dir)
    {
        ArgumentNullException.ThrowIfNull(dir);

        return new DirectorySummaryDto(
            Id: dir.Id,
            Name: dir.Name,
            ParentId: dir.ParentId ?? Guid.Empty,
            CreatedAt: dir.CreatedAt,
            UpdatedAt: dir.UpdatedAt ?? dir.CreatedAt,
            OwnerUserDto: dir.Owner?.ToUserDto() ?? new UserDto()
        );
    }

    public static UserDto ToUserDto(this ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Name = user.Name ?? string.Empty
        };
    }

    public static FileSummary ToFileSummary(this File file)
    {

        ArgumentNullException.ThrowIfNull(file);

        return new FileSummary(
            Id: file.Id,
            FileName: file.Name,
            MimeType: file.MimeType,
            HasPreview: file.HasPreview
        );
    }
}
