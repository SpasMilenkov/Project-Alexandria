using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public class StreamingRepresentation : IBase
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public TranspilationJob Job { get; set; } = null!;

    public StreamCodec Codec { get; set; }

    // video-specific, null for audio representations
    public int? Width { get; set; }
    public int? Height { get; set; }

    // audio bitrate in kbps, applies to both audio tracks in video and standalone audio
    public int? BitrateKbps { get; set; }

    public RepresentationStatus Status { get; set; } = RepresentationStatus.Pending;

    // relative path root in the streaming bucket, e.g. "{fileId}/v/1080p_av1"
    public string? SegmentPrefix { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}