namespace Alexandria.Dto.Files;

/// <summary>What the server measured from an upload's sealed bytes, ready to be committed.</summary>
public sealed record VerifiedUpload
{
    public required Guid UploadId { get; init; }
    public required Guid UploadedBy { get; init; }
    public required string TempObjectKey { get; init; }
    public required byte[] Hash { get; init; }
    public required long Size { get; init; }
    public required byte[] Header { get; init; }
}
