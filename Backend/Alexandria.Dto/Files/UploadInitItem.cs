namespace Alexandria.Dto.Files;

public sealed record UploadInitItem
{
    public required string ContentType { get; init; }
    public required string Hash { get; init; }
    public required long ContentLength { get; init; }
    public required Guid? DirectoryId { get; init; }
}
