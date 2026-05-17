using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming;

/// <summary>
/// Describes the local filesystem output produced by a single transpilation pass
/// </summary>
public class TranspilationOutput
{
    public string DashDirectory { get; init; } = string.Empty;
    public string RootDirectory { get; init; } = string.Empty;
    public IReadOnlyList<ProducedLane> Lanes { get; init; } = [];
}

public record ProducedLane(StreamCodec Codec, int BitrateKbps, int? Width = null, int? Height = null);