using System.Text.Json;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Policies;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Policies;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit;

public class PolicyRuleParametersResolverTests
{
    private static readonly Guid KnownRuleId = Guid.Parse("c0ffeec0-ffee-c0ff-eec0-ffeec0ffeec0");

    // Happy path — TranscodeParameters
    // System.Text.Json serializes enums as integers by default (Kbps128 = 1, P720 = 2, etc.)
    [Fact]
    public void Resolve_transcode_with_all_fields()
    {
        var rule = CreateRule(PolicyActionType.Transcode, """
                                                          {
                                                              "audioRungs": [1, 4],
                                                              "videoRungs": [2, 3],
                                                              "generateThumbnail": true
                                                          }
                                                          """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<TranscodeParameters>();
        var t = (TranscodeParameters)result;
        t.AudioRungs.Should().BeEquivalentTo([AudioRung.Kbps128, AudioRung.Kbps320]);
        t.VideoRungs.Should().BeEquivalentTo([VideoRung.P720, VideoRung.P1080]);
        t.GenerateThumbnail.Should().BeTrue();
    }

    [Fact]
    public void Resolve_transcode_with_empty_arrays()
    {
        var rule = CreateRule(PolicyActionType.Transcode, """
                                                          { "audioRungs": [], "videoRungs": [], "generateThumbnail": false }
                                                          """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<TranscodeParameters>();
        var t = (TranscodeParameters)result;
        t.AudioRungs.Should().BeEmpty();
        t.VideoRungs.Should().BeEmpty();
        t.GenerateThumbnail.Should().BeFalse();
    }

    [Fact]
    public void Resolve_transcode_audio_only()
    {
        var rule = CreateRule(PolicyActionType.Transcode, """
                                                          { "audioRungs": [2], "videoRungs": [], "generateThumbnail": false }
                                                          """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<TranscodeParameters>();
        var t = (TranscodeParameters)result;
        t.AudioRungs.Should().BeEquivalentTo([AudioRung.Kbps192]);
        t.VideoRungs.Should().BeEmpty();
        t.GenerateThumbnail.Should().BeFalse();
    }

    [Fact]
    public void Resolve_transcode_all_rungs()
    {
        var rule = CreateRule(PolicyActionType.Transcode, """
                                                          {
                                                              "audioRungs": [0, 1, 2, 3, 4],
                                                              "videoRungs": [0, 1, 2, 3, 4, 5],
                                                              "generateThumbnail": true
                                                          }
                                                          """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<TranscodeParameters>();
        var t = (TranscodeParameters)result;
        t.AudioRungs.Should().BeEquivalentTo(
            [AudioRung.Kbps96, AudioRung.Kbps128, AudioRung.Kbps192, AudioRung.Kbps256, AudioRung.Kbps320]);
        t.VideoRungs.Should().BeEquivalentTo(
            [VideoRung.P360, VideoRung.P480, VideoRung.P720, VideoRung.P1080, VideoRung.P1440, VideoRung.P2160]);
    }

    // Happy path — BackupParameters
    [Fact]
    public void Resolve_backup_daily()
    {
        var rule = CreateRule(PolicyActionType.Backup, """
                                                       { "destinationPath": "/backups/media/", "frequency": 0 }
                                                       """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<BackupParameters>();
        var b = (BackupParameters)result;
        b.DestinationPath.Should().Be("/backups/media/");
        b.Frequency.Should().Be(BackupFrequency.Daily);
    }

    [Fact]
    public void Resolve_backup_weekly()
    {
        var rule = CreateRule(PolicyActionType.Backup, """
                                                       { "destinationPath": "/backups/docs/", "frequency": 2 }
                                                       """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<BackupParameters>();
        var b = (BackupParameters)result;
        b.DestinationPath.Should().Be("/backups/docs/");
        b.Frequency.Should().Be(BackupFrequency.Weekly);
    }

    [Fact]
    public void Resolve_backup_every_3_months()
    {
        var rule = CreateRule(PolicyActionType.Backup, """
                                                       { "destinationPath": "/archive/", "frequency": 4 }
                                                       """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<BackupParameters>();
        var b = (BackupParameters)result;
        b.DestinationPath.Should().Be("/archive/");
        b.Frequency.Should().Be(BackupFrequency.Every3Months);
    }

    // Happy path — AutoTagParameters
    [Fact]
    public void Resolve_autotag_file_name()
    {
        var rule = CreateRule(PolicyActionType.AutoTag, """
                                                        { "source": 0 }
                                                        """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<AutoTagParameters>();
        var a = (AutoTagParameters)result;
        a.Source.Should().Be(TagSource.FileName);
    }

    [Fact]
    public void Resolve_autotag_file_metadata()
    {
        var rule = CreateRule(PolicyActionType.AutoTag, """
                                                        { "source": 1 }
                                                        """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<AutoTagParameters>();
        var a = (AutoTagParameters)result;
        a.Source.Should().Be(TagSource.FileMetadata);
    }

    // Edge cases — should succeed
    [Fact]
    public void Resolve_extra_fields_ignored()
    {
        var rule = CreateRule(PolicyActionType.Transcode, """
                                                          {
                                                              "audioRungs": [0],
                                                              "videoRungs": [],
                                                              "generateThumbnail": true,
                                                              "extraField": "ignored"
                                                          }
                                                          """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<TranscodeParameters>();
        var t = (TranscodeParameters)result;
        t.AudioRungs.Should().BeEquivalentTo([AudioRung.Kbps96]);
        t.GenerateThumbnail.Should().BeTrue();
    }

    [Fact]
    public void Resolve_null_destination_path()
    {
        var rule = CreateRule(PolicyActionType.Backup, """
                                                       { "destinationPath": null, "frequency": 0 }
                                                       """);

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<BackupParameters>();
        var b = (BackupParameters)result;
        b.DestinationPath.Should().BeNull();
        b.Frequency.Should().Be(BackupFrequency.Daily);
    }

    [Fact]
    public void Resolve_missing_optional_fields_get_defaults()
    {
        // Empty JSON means all properties get default values
        var rule = CreateRule(PolicyActionType.Transcode, "{}");

        var result = PolicyRuleParametersResolver.Resolve(rule);

        result.Should().BeOfType<TranscodeParameters>();
        var t = (TranscodeParameters)result;
        t.AudioRungs.Should().BeNull();
        t.VideoRungs.Should().BeNull();
        t.GenerateThumbnail.Should().BeFalse();
    }

    [Fact]
    public void Resolve_via_entity_and_dto_are_equivalent()
    {
        var json = """
                   { "audioRungs": [1], "videoRungs": [3], "generateThumbnail": false }
                   """;

        var doc = JsonDocument.Parse(json);
        var rule = new PolicyRule
        {
            Id = KnownRuleId,
            ActionType = PolicyActionType.Transcode,
            Parameters = JsonDocument.Parse(json)
        };
        var dto = new PolicyRuleDto
        {
            Id = KnownRuleId,
            ActionType = PolicyActionType.Transcode,
            Parameters = doc.RootElement.Clone()
        };

        var fromRule = (TranscodeParameters)PolicyRuleParametersResolver.Resolve(rule);
        var fromDto = (TranscodeParameters)PolicyRuleParametersResolver.ResolveFromDto(dto);

        fromRule.Should().BeEquivalentTo(fromDto);
    }

    // Unhappy path — invalid PolicyActionType
    [Fact]
    public void Resolve_unknown_action_type_throws()
    {
        var rule = CreateRule((PolicyActionType)99, """
                                                    { "audioRungs": [0], "videoRungs": [], "generateThumbnail": true }
                                                    """);

        var act = () => PolicyRuleParametersResolver.Resolve(rule);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Unknown policy action type*");
    }

    // Unhappy path — deserialization failures
    [Fact]
    public void Resolve_string_as_enum_integer_throws()
    {
        // STJ expects integers for enums by default; a string like "Kbps128" cannot convert
        var rule = CreateRule(PolicyActionType.Transcode, """
                                                          { "audioRungs": ["Kbps128"], "videoRungs": [], "generateThumbnail": true }
                                                          """);

        var act = () => PolicyRuleParametersResolver.Resolve(rule);

        act.Should().Throw<InvalidPolicyParametersException>();
    }

    [Fact]
    public void Resolve_wrong_json_type_throws()
    {
        var rule = CreateRule(PolicyActionType.Transcode, """
                                                          { "audioRungs": "not-an-array", "videoRungs": [], "generateThumbnail": true }
                                                          """);

        var act = () => PolicyRuleParametersResolver.Resolve(rule);

        act.Should().Throw<InvalidPolicyParametersException>();
    }

    [Fact]
    public void Resolve_null_json_value_throws()
    {
        var doc = JsonDocument.Parse("null");
        var rule = new PolicyRule
        {
            Id = KnownRuleId,
            ActionType = PolicyActionType.Transcode,
            Parameters = doc
        };

        var act = () => PolicyRuleParametersResolver.Resolve(rule);

        act.Should().Throw<InvalidPolicyParametersException>()
            .WithMessage("*" + KnownRuleId.ToString() + "*");
    }

    [Fact]
    public void Resolve_json_primitive_where_object_expected_throws()
    {
        var rule = CreateRule(PolicyActionType.Transcode, "\"just a string\"");

        var act = () => PolicyRuleParametersResolver.Resolve(rule);

        act.Should().Throw<InvalidPolicyParametersException>();
    }

    [Fact]
    public void Resolve_null_boolean_throws()
    {
        var rule = CreateRule(PolicyActionType.Transcode, """
                                                          { "audioRungs": [], "videoRungs": [], "generateThumbnail": null }
                                                          """);

        var act = () => PolicyRuleParametersResolver.Resolve(rule);

        act.Should().Throw<InvalidPolicyParametersException>();
    }

    // Rule ID preservation
    [Theory]
    [InlineData(PolicyActionType.Transcode, """{ "audioRungs": false, "videoRungs": [], "generateThumbnail": true }""")]
    [InlineData(PolicyActionType.Backup, """{ "destinationPath": 123, "frequency": 0 }""")]
    [InlineData(PolicyActionType.AutoTag, """{ "source": false }""")]
    public void Exception_contains_rule_id(PolicyActionType actionType, string json)
    {
        var rule = CreateRule(actionType, json);

        var act = () => PolicyRuleParametersResolver.Resolve(rule);

        act.Should().Throw<InvalidPolicyParametersException>()
            .WithMessage("*" + KnownRuleId.ToString() + "*");
    }

    // Helpers
    private static PolicyRule CreateRule(PolicyActionType actionType, string json)
    {
        return new PolicyRule
        {
            Id = KnownRuleId,
            ActionType = actionType,
            Parameters = JsonDocument.Parse(json)
        };
    }
}