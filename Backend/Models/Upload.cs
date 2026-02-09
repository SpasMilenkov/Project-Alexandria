using System.Numerics;
using Models.Enumerators;

namespace Models;

public class Upload : IBase
{
    public Guid Id { get; set; }
    public UploadStatus Status { get; set; }
    public DateTime? FinishedAt { get; set; }
    public byte[] Hash { get; set; } = [];
    public required string TempObjectKey { get; set; }
    public BigInteger Size { get; set; }
    public required string MimeType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; }
}