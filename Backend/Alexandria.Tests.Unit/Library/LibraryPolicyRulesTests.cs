using Alexandria.Common.Config;
using Alexandria.Common.Library;
using Alexandria.Common.Policies;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Library;
using Alexandria.Services.Storage.Library;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Library;

public class LibraryPolicyRulesTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static ProcessingCapabilities Capabilities(bool streaming = true, bool autoTagging = true) => new()
    {
        AdaptiveStreaming = streaming,
        Autotagging = autoTagging,
        AudioAnalysis = true,
    };

    private static LibraryFolderChoice MusicChoice(bool automation = true, bool autoTag = true) => new()
    {
        Kind = LibraryFolderKind.Music,
        Name = "Music",
        AutomationEnabled = automation,
        AudioRungs = [AudioRung.Kbps320, AudioRung.Kbps192, AudioRung.Kbps320],
        VideoRungs = [VideoRung.P1080],
        AutoTag = autoTag,
    };

    private static TranscodeParameters TranscodeOf(Alexandria.Data.Models.PolicyRule rule)
    {
        return (TranscodeParameters)PolicyRuleParametersResolver.Resolve(rule);
    }

    [Fact]
    public void Build_musicWithEverything_transcodesAudioThenAutoTags()
    {
        var rules = LibraryPolicyRules.Build(
            LibraryFolderPresets.Get(LibraryFolderKind.Music), MusicChoice(), Capabilities(), UserId);

        rules.Select(r => r.ActionType).Should()
            .Equal(PolicyActionType.Transcode, PolicyActionType.AutoTag);

        rules.Should().AllSatisfy(r =>
        {
            r.TriggerType.Should().Be(PolicyTriggerType.FileGroup);
            r.TriggerValue.Should().Be(FileGroups.Audio);
            r.ApplyOnNewVersion.Should().BeTrue();
            r.UpdatedBy.Should().Be(UserId);
        });

        rules[0].Priority.Should().BeLessThan(rules[1].Priority);
    }

    [Fact]
    public void Build_music_keepsOnlyDistinctAudioRungsAndDropsVideoRungs()
    {
        var rules = LibraryPolicyRules.Build(
            LibraryFolderPresets.Get(LibraryFolderKind.Music), MusicChoice(), Capabilities(), UserId);

        var parameters = TranscodeOf(rules[0]);

        parameters.AudioRungs.Should().Equal(AudioRung.Kbps192, AudioRung.Kbps320);
        parameters.VideoRungs.Should().BeEmpty();
        parameters.GenerateThumbnail.Should().BeTrue();
    }

    [Fact]
    public void Build_movies_transcodesVideoOnly()
    {
        var choice = new LibraryFolderChoice
        {
            Kind = LibraryFolderKind.Movies,
            Name = "Movies",
            AutomationEnabled = true,
            AudioRungs = [AudioRung.Kbps320],
            VideoRungs = [VideoRung.P720, VideoRung.P1080],
            AutoTag = true,
        };

        var rules = LibraryPolicyRules.Build(
            LibraryFolderPresets.Get(LibraryFolderKind.Movies), choice, Capabilities(), UserId);

        rules.Should().ContainSingle();
        rules[0].TriggerValue.Should().Be(FileGroups.Videos);

        var parameters = TranscodeOf(rules[0]);
        parameters.VideoRungs.Should().Equal(VideoRung.P720, VideoRung.P1080);
        parameters.AudioRungs.Should().BeEmpty();
    }

    [Fact]
    public void Build_automationOff_createsNothing()
    {
        var rules = LibraryPolicyRules.Build(
            LibraryFolderPresets.Get(LibraryFolderKind.Music), MusicChoice(automation: false), Capabilities(), UserId);

        rules.Should().BeEmpty();
    }

    [Fact]
    public void Build_streamingOff_keepsOnlyAutoTag()
    {
        var rules = LibraryPolicyRules.Build(
            LibraryFolderPresets.Get(LibraryFolderKind.Music), MusicChoice(), Capabilities(streaming: false), UserId);

        rules.Select(r => r.ActionType).Should().Equal(PolicyActionType.AutoTag);
    }

    [Fact]
    public void Build_autoTaggingInactive_keepsOnlyTranscode()
    {
        var rules = LibraryPolicyRules.Build(
            LibraryFolderPresets.Get(LibraryFolderKind.Music), MusicChoice(), Capabilities(autoTagging: false), UserId);

        rules.Select(r => r.ActionType).Should().Equal(PolicyActionType.Transcode);
    }

    [Fact]
    public void Build_autoTagNotChosen_keepsOnlyTranscode()
    {
        var rules = LibraryPolicyRules.Build(
            LibraryFolderPresets.Get(LibraryFolderKind.Music), MusicChoice(autoTag: false), Capabilities(), UserId);

        rules.Select(r => r.ActionType).Should().Equal(PolicyActionType.Transcode);
    }

    [Fact]
    public void Build_noRungsChosen_skipsTranscode()
    {
        var choice = MusicChoice() with { AudioRungs = [] };

        var rules = LibraryPolicyRules.Build(
            LibraryFolderPresets.Get(LibraryFolderKind.Music), choice, Capabilities(), UserId);

        rules.Select(r => r.ActionType).Should().Equal(PolicyActionType.AutoTag);
    }

    [Theory]
    [InlineData(LibraryFolderKind.Pictures)]
    [InlineData(LibraryFolderKind.Videos)]
    [InlineData(LibraryFolderKind.Documents)]
    [InlineData(LibraryFolderKind.Notes)]
    public void Build_plainFolder_createsNothingEvenWithAutomationOn(LibraryFolderKind kind)
    {
        var choice = MusicChoice() with { Kind = kind };

        var rules = LibraryPolicyRules.Build(LibraryFolderPresets.Get(kind), choice, Capabilities(), UserId);

        rules.Should().BeEmpty();
    }

    [Fact]
    public void Presets_coverEveryKindOnce()
    {
        LibraryFolderPresets.All.Select(p => p.Kind).Should()
            .BeEquivalentTo(Enum.GetValues<LibraryFolderKind>());
    }
}
