using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class Preview : IBase
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.MediumString)]
    public required string MimeType { get; set; }

    [Range(ValidationConstants.FileConstants.MinFileSize,
        ValidationConstants.FileConstants.MaxFileSize,
        ErrorMessage = ValidationConstants.ErrorMessages.FileSizeRange)]
    public long Size { get; set; }

    public PreviewKind Kind { get; set; }
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    [StringLength(ValidationConstants.StringLengths.UserId)]
    public Guid? UpdatedBy { get; set; }

    public Guid VersionId { get; set; }
    public FileVersion? Version { get; set; }

    /// <summary>
    /// S3 key inside the preview bucket (e.g. "previews/abc123" or "thumbnails/abc123").
    /// Tracked so preview artifacts can be deleted without touching job history.
    /// </summary>
    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.MediumString)]
    public required string ObjectKey { get; set; } = string.Empty;
}