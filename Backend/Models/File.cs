using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace Models;

public class File : IBase
{
    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.MediumString)]
    public required string Name { get; set; }

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.ExtraLongString)]
    public required string Path { get; set; }

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.MediumString)]
    public required string MimeType { get; init; }

    [Range(ValidationConstants.FileConstants.MinFileSize,
        ValidationConstants.FileConstants.MaxFileSize,
        ErrorMessage = ValidationConstants.ErrorMessages.FileSizeRange)]
    public BigInteger Size { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid Id { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public bool HasPreview { get; set; } = false;
    public DateTime? PreviewGeneratedAt { get; set; }

    [StringLength(ValidationConstants.StringLengths.UserId)]
    public string? UpdatedBy { get; set; }

    // Navigation property for related SignedUrls
    public virtual ICollection<SignedUrl> SignedUrls { get; set; } = new List<SignedUrl>();
}