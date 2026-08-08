using System.Text.Json.Nodes;

namespace Alexandria.Dto.Enrichment;

/// <summary>
/// A file's enrichment history: the newest outcome row per analyzer plus the batch attempts
/// it has been through. Shared by the file-facing and admin endpoints.
/// </summary>
public sealed class GetFileEnrichmentResponse
{
    public required Guid FileId { get; init; }

    public List<EnrichmentRowDto> Enrichments { get; init; } = [];

    public List<BatchRowDto> Batches { get; init; } = [];
}

/// <summary>
/// Newest <c>FileEnrichment</c> row for a single analyzer.
/// </summary>
public sealed class EnrichmentRowDto
{
    public required string Analyzer { get; init; }

    public required string Version { get; init; }

    public required JsonNode Payload { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// One batch-file attempt for a file.
/// </summary>
public sealed class BatchRowDto
{
    public required Guid BatchId { get; init; }

    public required string BatchStatus { get; init; }

    public required string FileStatus { get; init; }

    public string? ErrorDetail { get; init; }

    public DateTime CreatedAt { get; init; }
}