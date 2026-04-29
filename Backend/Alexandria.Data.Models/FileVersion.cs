using System.ComponentModel.DataAnnotations;

namespace Alexandria.Data.Models;

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

    //Encryption state
    public bool IsEncrypted { get; set; }
    public byte[]? EncryptionIv { get; set; }
    public byte[]? EncryptionSalt { get; set; }
    public byte[]? IntegrityTag { get; set; }
    public string? EncryptionHint { get; set; }

    /// <summary>
    /// Key derivation function version. 1 = PBKDF2-SHA256.
    /// Stored to allow future KDF migration without re-encrypting blobs.
    /// Null when IsClientEncrypted is false.
    /// </summary>
    public short? KdfVersion { get; set; }

    /// <summary>
    /// The number of iterations used when deryving the key with PBKDF2
    /// Used to for future reencryption if security requirements rise
    /// </summary>
    public int? IterationCount { get; set; }

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