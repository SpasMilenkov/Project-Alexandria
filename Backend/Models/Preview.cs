using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace Models;

public class Preview : IBase
{
    public Guid Id { get; set; }

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

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    [StringLength(ValidationConstants.StringLengths.UserId)]
    public Guid? UpdatedBy { get; set; }

    public Guid FileId { get; set; }
    public File? File { get; set; }
}