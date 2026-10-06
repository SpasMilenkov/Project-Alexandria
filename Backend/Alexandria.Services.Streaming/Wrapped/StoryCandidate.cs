using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>An optional story card with the topic, score and display order used to pick the final deck.</summary>
internal sealed record StoryCandidate(WrappedCard Card, string Topic, int Score, int Order);
