using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

public sealed class StreamingRepresentationDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public StreamCodec Codec { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? BitrateKbps { get; init; }
    public RepresentationStatus Status { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}