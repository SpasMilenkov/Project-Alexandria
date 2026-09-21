using Alexandria.Dto.Files.Streaming.Shuffle;

namespace Alexandria.Services.Streaming.Shuffle;

public static class ShuffleRanker
{
    public const double NeverHeardWeight = 4.0;
    public const double BaseWeight = 1.0;
    public const double RecencyGain = 3.0;
    public const double HalfLifeDays = 7.0;
    public const double MeaningfulListenSeconds = 30.0;

    public static double ComputeThreshold(double? durationSeconds)
    {
        if (!durationSeconds.HasValue
            || double.IsNaN(durationSeconds.Value)
            || double.IsInfinity(durationSeconds.Value)
            || durationSeconds.Value <= 0)
            return MeaningfulListenSeconds;

        return Math.Min(MeaningfulListenSeconds, durationSeconds.Value / 2);
    }

    public static double ComputeWeight(DateTime? lastHeardUtc, DateTime asOfUtc)
    {
        if (!lastHeardUtc.HasValue)
            return NeverHeardWeight;

        var days = Math.Max(0, (asOfUtc - lastHeardUtc.Value).TotalDays);
        return BaseWeight + RecencyGain * (1 - Math.Pow(2, -days / HalfLifeDays));
    }

    public static DateTime? GetLastHeard(
        IEnumerable<ShuffleListenRow> rowsForFile, double? durationSeconds, DateTime asOfUtc)
    {
        var threshold = ComputeThreshold(durationSeconds);
        DateTime? lastHeard = null;

        foreach (var row in rowsForFile)
        {
            if (row.ListenedSeconds < threshold)
                continue;
            if (lastHeard.HasValue && row.EndedAtUtc <= lastHeard.Value)
                continue;
            lastHeard = row.EndedAtUtc;
        }

        if (lastHeard.HasValue && lastHeard.Value > asOfUtc)
            return asOfUtc;

        return lastHeard;
    }

    public static IReadOnlyList<PlaybackSourceEntryRef> Rank(
        IReadOnlyList<ShuffleCandidate> candidates,
        IReadOnlyList<ShuffleListenRow> listenRows,
        DateTime asOfUtc,
        bool isVideo,
        Random random,
        Guid? anchorFileId = null,
        Guid? anchorPlaylistItemId = null,
        Guid? avoidFirstFileId = null)
    {
        if (anchorFileId.HasValue && avoidFirstFileId.HasValue)
            throw new InvalidOperationException("Anchor and avoid-first cannot be combined.");

        if (candidates.Count == 0)
            return [];

        var anchor = ExtractAnchor(candidates, anchorFileId, anchorPlaylistItemId);
        var remaining = anchor.HasValue
            ? candidates.Where(c => c.Entry.FileId != anchor.Value.Entry.FileId).ToList()
            : candidates.ToList();

        var deduplicated = Deduplicate(remaining);
        var weights = ComputeWeights(deduplicated, listenRows, asOfUtc, isVideo);
        var ordered = WeightedOrder(deduplicated, weights, random);
        ApplyAvoidFirst(ordered, avoidFirstFileId);

        if (anchor.HasValue)
            ordered.Insert(0, anchor.Value.Entry);

        return ordered;
    }

    private readonly struct AnchorSelection(PlaybackSourceEntryRef entry, int index)
    {
        public PlaybackSourceEntryRef Entry { get; } = entry;
        public int Index { get; } = index;
    }

    private static AnchorSelection? ExtractAnchor(
        IReadOnlyList<ShuffleCandidate> candidates, Guid? anchorFileId, Guid? anchorPlaylistItemId)
    {
        if (!anchorFileId.HasValue)
        {
            if (anchorPlaylistItemId.HasValue)
                throw new InvalidOperationException("Anchor item requires an anchor file.");
            return null;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var entry = candidates[i].Entry;
            if (anchorPlaylistItemId.HasValue)
            {
                if (entry.PlaylistItemId == anchorPlaylistItemId.Value && entry.FileId == anchorFileId.Value)
                    return new AnchorSelection(entry, i);
            }
            else if (entry.FileId == anchorFileId.Value)
            {
                return new AnchorSelection(entry, i);
            }
        }

        throw new InvalidOperationException("Anchor is not part of the eligible source.");
    }

    private static List<ShuffleCandidate> Deduplicate(List<ShuffleCandidate> candidates)
    {
        var seen = new HashSet<Guid>();
        var deduplicated = new List<ShuffleCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (seen.Add(candidate.Entry.FileId))
                deduplicated.Add(candidate);
        }

        return deduplicated;
    }

    private static Dictionary<Guid, double> ComputeWeights(
        List<ShuffleCandidate> deduplicated,
        IReadOnlyList<ShuffleListenRow> listenRows,
        DateTime asOfUtc,
        bool isVideo)
    {
        var weights = new Dictionary<Guid, double>(deduplicated.Count);
        if (isVideo)
        {
            foreach (var candidate in deduplicated)
                weights[candidate.Entry.FileId] = BaseWeight;
            return weights;
        }

        var rowsByFile = new Dictionary<Guid, List<ShuffleListenRow>>();
        foreach (var row in listenRows)
        {
            if (!rowsByFile.TryGetValue(row.FileId, out var list))
            {
                list = [];
                rowsByFile[row.FileId] = list;
            }

            list.Add(row);
        }

        foreach (var candidate in deduplicated)
        {
            rowsByFile.TryGetValue(candidate.Entry.FileId, out var rows);
            var lastHeard = GetLastHeard(rows ?? [], candidate.DurationSeconds, asOfUtc);
            weights[candidate.Entry.FileId] = ComputeWeight(lastHeard, asOfUtc);
        }

        return weights;
    }

    private static List<PlaybackSourceEntryRef> WeightedOrder(
        List<ShuffleCandidate> deduplicated, Dictionary<Guid, double> weights, Random random)
    {
        var keys = new List<(PlaybackSourceEntryRef Entry, double Key)>(deduplicated.Count);
        foreach (var candidate in deduplicated)
        {
            var weight = weights[candidate.Entry.FileId];
            double draw;
            do
            {
                draw = random.NextDouble();
            } while (draw == 0);

            keys.Add((candidate.Entry, -Math.Log(draw) / weight));
        }

        return keys
            .OrderBy(k => k.Key)
            .ThenBy(k => k.Entry.FileId)
            .Select(k => k.Entry)
            .ToList();
    }

    private static void ApplyAvoidFirst(List<PlaybackSourceEntryRef> ordered, Guid? avoidFirstFileId)
    {
        if (!avoidFirstFileId.HasValue || ordered.Count < 2)
            return;
        if (ordered[0].FileId != avoidFirstFileId.Value)
            return;

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].FileId != avoidFirstFileId.Value)
            {
                (ordered[0], ordered[i]) = (ordered[i], ordered[0]);
                return;
            }
        }
    }
}