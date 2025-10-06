using System.ComponentModel.DataAnnotations;
using Models.Enumerators;

namespace Models;

public class SignedUrl : IBase
{
    public Guid Id { get; set; }

    public Guid FileId { get; set; }

    // Navigation property - EF will handle this
    public File FileInfo { get; set; } = null!;

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.MediumString)]
    public required string BucketName { get; set; }

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.LongString)]
    public required string ObjectName { get; set; }

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.ExtraLongString)]
    public required string Token { get; set; }

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    public FilePermission Permission { get; set; }

    public DateTime ExpiresAt { get; set; }

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.UserId)]
    public required string CreatorId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    [StringLength(ValidationConstants.StringLengths.UserId)]
    public string? UpdatedBy { get; set; }
}