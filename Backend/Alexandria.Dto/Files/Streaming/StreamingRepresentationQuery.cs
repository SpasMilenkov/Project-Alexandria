using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

public class StreamingRepresentationQuery
{
    public Guid? JobId { get; init; }
    public StreamCodec? Codec { get; init; }
    public RepresentationStatus? Status { get; init; }
    public int? MinHeight { get; init; }
    public int CurrentPage { get; init; } = 0;
    public int PageSize { get; init; } = 25;
}