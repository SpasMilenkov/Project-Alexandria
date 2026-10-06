using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Summaries;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Dto.OverviewSummaries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming.Wrapped;

public sealed partial class OverviewSummaryService(
    IOverviewSummaryRepository repository,
    IEnumerable<ISummaryGenerator> generators,
    TimeProvider time,
    IMemoryCache cache,
    ILogger<OverviewSummaryService> logger) : IOverviewSummaryService
{
    private static readonly TimeSpan LiveTtl = TimeSpan.FromHours(1);

    private static readonly MemoryCacheEntryOptions LiveOptions = new()
    {
        AbsoluteExpirationRelativeToNow = LiveTtl,
        Size = 1
    };

    private readonly Dictionary<SummaryKind, ISummaryGenerator> _generators = generators.ToDictionary(g => g.Kind);

    public Task<IReadOnlyList<OverviewSummaryHeaderDto>> ListAsync(
        Guid userId, SummaryKind kind, DateTime? from, DateTime? to, CancellationToken ct = default) =>
        repository.ListHeadersAsync(userId, kind, from, to, finalOnly: true, ct);

    public async Task<OverviewSummaryDto> GetAsync(
        Guid userId, SummaryKind kind, SummaryPeriod period, CancellationToken ct = default)
    {
        var generator = GetGenerator(kind);
        var existing = await repository.GetByPeriodAsync(userId, kind, period, ct);

        if (existing is { FinalizedAt: not null })
        {
            LogServingFrozen(kind, userId, period.Start);

            return ToDto(existing);
        }

        var key = CacheKey(userId, kind, period, generator.SchemaVersion);

        if (cache.TryGetValue(key, out OverviewSummaryDto? cached) && cached is not null)
        {
            LogServingLiveCached(kind, userId, period.Start);

            return cached;
        }

        var payload = await generator.GenerateAsync(userId, period, ct);
        var live = ToLiveDto(kind, period, payload, generator.SchemaVersion);

        cache.Set(key, live, LiveOptions);

        return live;
    }

    public async Task<OverviewSummaryDto> FinalizeAsync(
        Guid userId, SummaryKind kind, SummaryPeriod period, CancellationToken ct = default)
    {
        var generator = GetGenerator(kind);
        var now = time.GetUtcNow().UtcDateTime;

        if (!period.IsClosedAt(now))
        {
            throw new SummaryPeriodNotClosedException(kind, period);
        }

        var existing = await repository.GetByPeriodAsync(userId, kind, period, ct);

        if (existing is { FinalizedAt: not null })
        {
            LogAlreadyFrozen(kind, userId, period.Start);

            return ToDto(existing);
        }

        var payload = await generator.GenerateAsync(userId, period, ct);

        if (payload is WrappedPayload wrapped && wrapped.Deck.Cards.Count == 0)
        {
            return ToLiveDto(kind, period, payload, generator.SchemaVersion);
        }

        var candidate = new OverviewSummary
        {
            UserId = userId,
            Kind = kind,
            PeriodStart = period.Start,
            PeriodEnd = period.End,
            GeneratedAt = now,
            FinalizedAt = now,
            SchemaVersion = generator.SchemaVersion,
            PayloadJson = SummaryJson.Serialize(payload)
        };

        var outcome = await repository.UpsertAsync(candidate, ct);

        LogUpserted(kind, userId, period.Start, outcome, true);

        var stored = await repository.GetByPeriodAsync(userId, kind, period, ct)
            ?? throw new OverviewSummaryNotFoundException(userId, kind, period.Start);

        cache.Remove(CacheKey(userId, kind, period, generator.SchemaVersion));

        return ToDto(stored);
    }

    private ISummaryGenerator GetGenerator(SummaryKind kind)
    {
        if (_generators.TryGetValue(kind, out var generator))
        {
            return generator;
        }

        throw new InvalidOperationException($"No summary generator is registered for kind {kind}.");
    }

    private static string CacheKey(Guid userId, SummaryKind kind, SummaryPeriod period, int schema) =>
        $"overview:{userId}:{kind}:{period.Start:O}:{period.End:O}:{schema}";

    private OverviewSummaryDto ToLiveDto(
        SummaryKind kind, SummaryPeriod period, SummaryPayload payload, int schema) => new()
    {
        Id = Guid.Empty,
        Kind = kind,
        PeriodStart = period.Start,
        PeriodEnd = period.End,
        GeneratedAt = time.GetUtcNow().UtcDateTime,
        FinalizedAt = null,
        IsFinal = false,
        SchemaVersion = schema,
        Payload = payload
    };

    private static OverviewSummaryDto ToDto(OverviewSummary entity) => new()
    {
        Id = entity.Id,
        Kind = entity.Kind,
        PeriodStart = entity.PeriodStart,
        PeriodEnd = entity.PeriodEnd,
        GeneratedAt = entity.GeneratedAt,
        FinalizedAt = entity.FinalizedAt,
        IsFinal = entity.FinalizedAt is not null,
        SchemaVersion = entity.SchemaVersion,
        Payload = SummaryJson.Deserialize(entity.PayloadJson)
    };
}
