namespace DTO.Metrics;

public record ServerResourcesResponse(
    ProcessInfo Process,
    DateTimeOffset CheckedAt
);
