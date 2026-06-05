using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Dto.Extensions;

public static class StreamingRepresentationExtensions
{
    public static StreamingRepresentationDto ToResponse(this StreamingRepresentation representation)
        => new()
        {
            Id = representation.Id,
            JobId = representation.JobId,
            Codec = representation.Codec,
            Width = representation.Width,
            Height = representation.Height,
            BitrateKbps = representation.BitrateKbps,
            Status = representation.Status,
            CompletedAt = representation.CompletedAt
        };

    public static StreamingRepresentation ToEntity(this CreateStreamingRepresentationRequest request)
        => new()
        {
            Id = Guid.NewGuid(),
            JobId = request.JobId,
            Codec = request.Codec,
            Width = request.Width,
            Height = request.Height,
            BitrateKbps = request.BitrateKbps,
            Status = RepresentationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
}