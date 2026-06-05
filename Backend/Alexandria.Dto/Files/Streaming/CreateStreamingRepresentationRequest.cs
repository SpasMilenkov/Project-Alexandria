using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

public sealed class CreateStreamingRepresentationRequest
{
    public Guid JobId { get; init; }
    public StreamCodec Codec { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? BitrateKbps { get; init; }
}