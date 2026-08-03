using System.Linq.Expressions;
using Alexandria.Data.Models.Enumerators;
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
            f.FileTags!.Where(ft => ft.Source != TagSource.Suppressed && ft.Tag.DeletedAt == null)
                .Select(ft => new TagDto
                {
                    Id = ft.Tag.Id,
                    CreatedAt = ft.Tag.CreatedAt,
                    UpdatedAt = ft.Tag.UpdatedAt,
                    Name = ft.Tag.Name,
                    Color = ft.Tag.Color,
                    Icon = ft.Tag.Icon,
                    Description = ft.Tag.Description,
                    UserId = ft.Tag.OwnerId,
                    Source = ft.Source,
                    Confidence = ft.Confidence
                }).ToList(),
            new UserDto
            {
                Id = f.OwnerId,
                Name = f.Owner.Name,
                Email = f.Owner.Email
            }
        );
}