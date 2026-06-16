using System.ComponentModel.DataAnnotations;

namespace Alexandria.Data.Models;

public class SignedUrl : IBase
{
    public Guid Id { get; set; }

    public Guid FileId { get; set; }
    public File FileInfo { get; set; } = null!;

    [Required(ErrorMessage = ValidationConstants.ErrorMessages.Required)]
    [StringLength(ValidationConstants.StringLengths.ExtraLongString)]
    public required string Token { get; set; }

    public DateTime ExpiresAt { get; set; }
    public int AccessCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int? MaxAccessCount { get; set; }

    public required Guid CreatorId { get; set; }

    /// <summary>
    /// When set, this link always resolves to a specific version rather than the file's current version.
    /// Null means "follow the current version pointer on the file".
    /// </summary>
    public Guid? FileVersionId { get; set; }

    public FileVersion? PinnedVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    [StringLength(ValidationConstants.StringLengths.UserId)]
    public Guid? UpdatedBy { get; set; }
}