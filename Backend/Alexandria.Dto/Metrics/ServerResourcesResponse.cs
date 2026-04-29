namespace Alexandria.Dto.Metrics;

public record ServerResourcesResponse(
    ProcessInfo Process,
    DateTimeOffset CheckedAt
);