using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Services.Streaming.Shuffle;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class ShuffleRankerTests
{
    private static readonly DateTime AsOf = new(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FileC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ItemOne = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ItemTwo = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ItemThree = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private sealed class FixedRandom(params double[] values) : Random
    {
        private readonly Queue<double> _values = new(values);

        public override double NextDouble() =>
            _values.Count > 0 ? _values.Dequeue() : base.NextDouble();
    }

    private static ShuffleCandidate Candidate(Guid fileId, Guid? itemId = null, double? duration = 120.0) =>
        new()
        {
            Entry = new PlaybackSourceEntryRef
            {
                FileId = fileId,
                PlaylistItemId = itemId,
                TranspilationJobId = Guid.NewGuid(),
            },
            DurationSeconds = duration,
        };

    [Fact]
    public void Rank_with_empty_source_returns_empty()
    {
        ShuffleRanker.Rank([], [], AsOf, false, new Random(1)).Should().BeEmpty();
    }

    [Fact]
    public void Rank_with_single_track_returns_it()
    {
        var result = ShuffleRanker.Rank([Candidate(FileA)], [], AsOf, false, new Random(1));

        result.Should().ContainSingle().Which.FileId.Should().Be(FileA);
    }

    [Fact]
    public void Rank_returns_complete_unique_permutation()
    {
        var candidates = new[] { Candidate(FileA), Candidate(FileB), Candidate(FileC) };

        var result = ShuffleRanker.Rank(candidates, [], AsOf, false, new Random(42));

        result.Should().HaveCount(3);
        result.Select(r => r.FileId).Should().Contain([FileA, FileB, FileC]);
        result.Select(r => r.FileId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Rank_with_same_seed_is_deterministic()
    {
        var candidates = new[] { Candidate(FileA), Candidate(FileB), Candidate(FileC) };

        var first = ShuffleRanker.Rank(candidates, [], AsOf, false, new Random(7));
        var second = ShuffleRanker.Rank(candidates, [], AsOf, false, new Random(7));

        first.Select(r => r.FileId).Should().Equal(second.Select(r => r.FileId));
    }

    [Fact]
    public void Rank_with_equal_weights_and_equal_draws_breaks_ties_by_file_id()
    {
        var candidates = new[] { Candidate(FileB), Candidate(FileA) };

        var result = ShuffleRanker.Rank(candidates, [], AsOf, true, new FixedRandom(0.5, 0.5));

        result.Select(r => r.FileId).Should().ContainInOrder(FileA, FileB);
    }

    [Fact]
    public void Rank_with_supplied_draws_applies_weighted_keys_not_recency_sort()
    {
        var candidates = new[] { Candidate(FileA), Candidate(FileB) };
        var history = new[]
        {
            new ShuffleListenRow { FileId = FileA, ListenedSeconds = 60, EndedAtUtc = AsOf },
            new ShuffleListenRow { FileId = FileB, ListenedSeconds = 60, EndedAtUtc = AsOf.AddDays(-60) },
        };

        var recentFirst = ShuffleRanker.Rank(candidates, history, AsOf, false, new FixedRandom(0.5, 0.5));
        recentFirst[0].FileId.Should().Be(FileB);

        var upset = ShuffleRanker.Rank(candidates, history, AsOf, false, new FixedRandom(0.9, 0.1));
        upset[0].FileId.Should().Be(FileA);
    }

    [Fact]
    public void Rank_for_video_ignores_history()
    {
        var candidates = new[] { Candidate(FileA), Candidate(FileB), Candidate(FileC) };
        var history = new[]
        {
            new ShuffleListenRow { FileId = FileA, ListenedSeconds = 60, EndedAtUtc = AsOf },
        };

        var withHistory = ShuffleRanker.Rank(candidates, history, AsOf, true, new Random(11));
        var withoutHistory = ShuffleRanker.Rank(candidates, [], AsOf, true, new Random(11));

        withHistory.Select(r => r.FileId).Should().Equal(withoutHistory.Select(r => r.FileId));
    }

    [Fact]
    public void Rank_deduplicates_playlist_files_keeping_earliest_occurrence()
    {
        var candidates = new[]
        {
            Candidate(FileA, ItemOne),
            Candidate(FileB, ItemTwo),
            Candidate(FileA, ItemThree),
        };

        var result = ShuffleRanker.Rank(candidates, [], AsOf, false, new Random(3));

        result.Should().HaveCount(2);
        result.Select(r => r.FileId).Should().OnlyHaveUniqueItems();
        var fileA = result.Single(r => r.FileId == FileA);
        fileA.PlaylistItemId.Should().Be(ItemOne);
    }

    [Fact]
    public void Rank_with_file_anchor_prepends_chosen_occurrence_at_zero()
    {
        var candidates = new[] { Candidate(FileA), Candidate(FileB), Candidate(FileC) };

        var result = ShuffleRanker.Rank(candidates, [], AsOf, false, new Random(5), anchorFileId: FileB);

        result.Should().HaveCount(3);
        result[0].FileId.Should().Be(FileB);
        result.Skip(1).Select(r => r.FileId).Should().NotContain(FileB);
    }

    [Fact]
    public void Rank_with_file_anchor_on_duplicate_file_keeps_first_occurrence()
    {
        var candidates = new[]
        {
            Candidate(FileA, ItemOne),
            Candidate(FileB, ItemTwo),
            Candidate(FileA, ItemThree),
        };

        var result = ShuffleRanker.Rank(candidates, [], AsOf, false, new Random(5), anchorFileId: FileA);

        result[0].FileId.Should().Be(FileA);
        result[0].PlaylistItemId.Should().Be(ItemOne);
    }

    [Fact]
    public void Rank_with_exact_item_anchor_keeps_that_occurrence()
    {
        var candidates = new[]
        {
            Candidate(FileA, ItemOne),
            Candidate(FileB, ItemTwo),
            Candidate(FileA, ItemThree),
        };

        var result = ShuffleRanker.Rank(
            candidates, [], AsOf, false, new Random(5),
            anchorFileId: FileA, anchorPlaylistItemId: ItemThree);

        result[0].FileId.Should().Be(FileA);
        result[0].PlaylistItemId.Should().Be(ItemThree);
        result.Select(entry => entry.FileId).Should().OnlyHaveUniqueItems();
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Rank_with_missing_anchor_throws()
    {
        var candidates = new[] { Candidate(FileA) };

        var act = () => ShuffleRanker.Rank(candidates, [], AsOf, false, new Random(1), anchorFileId: FileB);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rank_with_avoid_first_swaps_boundary_repeat()
    {
        var candidates = new[] { Candidate(FileA), Candidate(FileB), Candidate(FileC) };
        var baseline = ShuffleRanker.Rank(candidates, [], AsOf, false, new FixedRandom(0.1, 0.5, 0.9));
        var avoided = ShuffleRanker.Rank(
            candidates, [], AsOf, false, new FixedRandom(0.1, 0.5, 0.9), avoidFirstFileId: baseline[0].FileId);

        avoided.Should().HaveCount(3);
        avoided[0].FileId.Should().NotBe(baseline[0].FileId);
        avoided.Select(r => r.FileId).Should().Contain([FileA, FileB, FileC]);
        avoided.Should().Contain(r => r.FileId == baseline[0].FileId);
    }

    [Fact]
    public void Rank_with_missing_avoid_first_is_harmless()
    {
        var candidates = new[] { Candidate(FileA), Candidate(FileB) };

        var result = ShuffleRanker.Rank(
            candidates, [], AsOf, false, new Random(9), avoidFirstFileId: Guid.NewGuid());

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Rank_with_single_track_and_avoid_first_keeps_it()
    {
        var result = ShuffleRanker.Rank(
            [Candidate(FileA)], [], AsOf, false, new Random(1), avoidFirstFileId: FileA);

        result.Should().ContainSingle().Which.FileId.Should().Be(FileA);
    }

    [Fact]
    public void Rank_with_anchor_and_avoid_first_throws()
    {
        var candidates = new[] { Candidate(FileA), Candidate(FileB) };

        var act = () => ShuffleRanker.Rank(
            candidates, [], AsOf, false, new Random(1), anchorFileId: FileA, avoidFirstFileId: FileB);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rank_heavier_weight_wins_about_eighty_percent_of_seeded_trials()
    {
        var recent = Candidate(FileA);
        var old = Candidate(FileB);
        var candidates = new[] { recent, old };
        var history = new[]
        {
            new ShuffleListenRow { FileId = FileA, ListenedSeconds = 60, EndedAtUtc = AsOf },
        };

        var random = new Random(12345);
        var heavyFirst = 0;
        const int trials = 10000;
        for (var i = 0; i < trials; i++)
        {
            var result = ShuffleRanker.Rank(candidates, history, AsOf, false, random);
            if (result[0].FileId == FileB)
                heavyFirst++;
        }

        var rate = (double)heavyFirst / trials;
        rate.Should().BeInRange(0.75, 0.85);
    }
}