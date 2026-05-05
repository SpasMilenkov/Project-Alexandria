using System.Linq.Expressions;
using Alexandria.Dto.Files;
using Alexandria.Dto.Tags;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Repositories.Projections;

public static class FileProjections
{
    public static Expression<Func<File, FileResult>> ToFileResult =>
        f => new FileResult(
            f.Id,
            f.Name,
            f.MimeType,
            f.DirectoryId,
            f.CreatedAt,
            f.UpdatedAt,
            f.DeletedAt,
            new FileVersionDto(
                f.CurrentVersion.Id,
                f.CurrentVersion.Size,
                f.CurrentVersion.MimeType,
                f.CurrentVersion.VersionNumber,
                f.CurrentVersion.CreatedAt,
                f.DeletedAt == null,
                f.CurrentVersion.IsEncrypted
            ),
            f.Tags.Where(t => t.DeletedAt == null).Select(t => new TagDto
            {
                Id = t.Id,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                Name = t.Name,
                Color = t.Color,
                Icon = t.Icon,
                Description = t.Description,
                UserId = t.OwnerId
            }).ToList(),
            new UserDto
            {
                Id = f.OwnerId,
                Name = f.Owner.Name,
                Email = f.Owner.Email
            }
        );
}