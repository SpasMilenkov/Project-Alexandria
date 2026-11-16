using System.Text.Json;
using Alexandria.Common.Config;
using Alexandria.Common.Library;
using Alexandria.Common.Policies;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Library;

namespace Alexandria.Services.Storage.Library;

/// <summary>Turns a starter folder choice into the policy rules this installation can run.</summary>
internal static class LibraryPolicyRules
{
    // Transcoding runs before tagging so a fresh upload is streamable as early as possible.
    internal const int TranscodePriority = 10;
    internal const int AutoTagPriority = 25;

    private static readonly JsonSerializerOptions ParameterOptions = new(JsonSerializerDefaults.Web);

    public static List<PolicyRule> Build(
        LibraryFolderPreset preset,
        LibraryFolderChoice choice,
        ProcessingCapabilities capabilities,
        Guid userId)
    {
        var rules = new List<PolicyRule>();
        if (!choice.AutomationEnabled) return rules;

        var transcode = BuildTranscode(preset, choice, capabilities, userId);
        if (transcode is not null) rules.Add(transcode);

        var autoTag = BuildAutoTag(preset, choice, capabilities, userId);
        if (autoTag is not null) rules.Add(autoTag);

        return rules;
    }

    private static PolicyRule? BuildTranscode(
        LibraryFolderPreset preset,
        LibraryFolderChoice choice,
        ProcessingCapabilities capabilities,
        Guid userId)
    {
        if (preset.TranscodeGroup is null || !capabilities.AdaptiveStreaming) return null;

        AudioRung[] audioRungs = [];
        VideoRung[] videoRungs = [];

        if (preset.TranscodeGroup == FileGroups.Audio)
            audioRungs = choice.AudioRungs.Distinct().Order().ToArray();
        else if (preset.TranscodeGroup == FileGroups.Videos)
            videoRungs = choice.VideoRungs.Distinct().Order().ToArray();

        if (audioRungs.Length == 0 && videoRungs.Length == 0) return null;

        var parameters = new TranscodeParameters(audioRungs, videoRungs, GenerateThumbnail: true);

        return new PolicyRule
        {
            Id = Guid.NewGuid(),
            ActionType = PolicyActionType.Transcode,
            TriggerType = PolicyTriggerType.FileGroup,
            TriggerValue = preset.TranscodeGroup,
            Priority = TranscodePriority,
            ApplyOnNewVersion = true,
            Parameters = JsonSerializer.SerializeToDocument(parameters, ParameterOptions),
            UpdatedBy = userId,
        };
    }

    private static PolicyRule? BuildAutoTag(
        LibraryFolderPreset preset,
        LibraryFolderChoice choice,
        ProcessingCapabilities capabilities,
        Guid userId)
    {
        if (preset.AutoTagGroup is null || !choice.AutoTag || !capabilities.AutoTaggingActive) return null;

        // Source is ignored by the trigger today; FileMetadata matches what the rule editor sends.
        var parameters = new AutoTagParameters(TagSource.FileMetadata);

        return new PolicyRule
        {
            Id = Guid.NewGuid(),
            ActionType = PolicyActionType.AutoTag,
            TriggerType = PolicyTriggerType.FileGroup,
            TriggerValue = preset.AutoTagGroup,
            Priority = AutoTagPriority,
            ApplyOnNewVersion = true,
            Parameters = JsonSerializer.SerializeToDocument(parameters, ParameterOptions),
            UpdatedBy = userId,
        };
    }
}
