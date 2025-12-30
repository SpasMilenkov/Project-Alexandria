using System.ComponentModel.DataAnnotations;
using System.Numerics;
using Models.Enumerators;

namespace Models;

public class File : IBase
{
    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.MediumString)]
    public required string Name { get; set; }

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.MediumString)]
    public required string MimeType { get; init; }

    public DateTime CreatedAt { get; set; }

    public Guid Id { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
    public bool HasPreview { get; set; } = false;
    public DateTime? PreviewGeneratedAt { get; set; }

    [StringLength(ValidationConstants.StringLengths.UserId)]
    public Guid? UpdatedBy { get; set; }

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<SignedUrl> SignedUrls { get; set; } = new List<SignedUrl>();
    
    public Guid PreviewId { get; set; }
    public Preview? Preview { get; set; }
    public ApplicationUser? Owner { get; set; }
    public Guid OwnerId { get; set; }
    public Directory? Directory { get; set; }
    public Guid? DirectoryId { get; set; }
    
    public Guid? CurrentVersionId { get; set; }
    public FileVersion CurrentVersion { get; set; } = null!;
}