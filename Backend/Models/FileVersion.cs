using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace Models;

public class FileVersion : IBase
{
    public Guid Id { get; set; }

    public byte[] ContentHash { get; set; } = [];
    [Range(ValidationConstants.FileConstants.MinFileSize,
        ValidationConstants.FileConstants.MaxFileSize,
        ErrorMessage = ValidationConstants.ErrorMessages.FileSizeRange)]
    public required long Size { get; set; }
    public required int VersionNumber { get; set; }

    //Presentation data
    public required string MimeType { get; set; }
    
    // Lifecycle
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    
    // Relations
    public Guid ContentObjectId { get; set; }
    public ContentObject ContentObject { get; set; } = null!;
    public Guid FileId { get; set; }
    public File File { get; set; } = null!;
}