namespace Alexandria.Dto.Enrichment;

public sealed record EnrichmentBackfillRequest(Guid PolicyId, Guid RuleId, DateTimeOffset Cutoff);
