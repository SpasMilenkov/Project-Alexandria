using System.Text.Json;
using Alexandria.Common.Config;
using Alexandria.Common.Library;
using Alexandria.Common.Policies;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Library;
using Alexandria.Dto.Policies;

namespace Alexandria.Services.Storage.Library;

/// <summary>Turns a starter folder choice into the policy rules this installation can run.</summary>
internal static class LibraryPolicyRules
{
    // Transcoding runs before tagging so a fresh upload is streamable as early as possible.
    internal const int TranscodePriority = 10;
    internal const int AutoTagPriority = 25;

    private static readonly JsonSerializerOptions ParameterOptions = new(JsonSerializerDefaults.Web);

    public static List<CreatePolicyRuleRequest> Build(
        LibraryFolderPreset preset,
        LibraryFolderChoice choice,
        ProcessingCapabilities capabilities)
    {
        var rules = new List<CreatePolicyRuleRequest>();
        if (!choice.AutomationEnabled) return rules;

        var transcode = BuildTranscode(preset, choice, capabilities);
        if (transcode is not null) rules.Add(transcode);

        var autoTag = BuildAutoTag(preset, choice, capabilities);
        if (autoTag is not null) rules.Add(autoTag);

        return rules;
    }

    private static CreatePolicyRuleRequest? BuildTranscode(
        LibraryFolderPreset preset,
        LibraryFolderChoice choice,
        ProcessingCapabilities capabilities)
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

        return new CreatePolicyRuleRequest
        {
            ActionType = PolicyActionType.Transcode,
            TriggerType = PolicyTriggerType.FileGroup,
            TriggerValue = preset.TranscodeGroup,
            Priority = TranscodePriority,
            ApplyOnNewVersion = true,
            Parameters = JsonSerializer.SerializeToDocument(parameters, ParameterOptions),
        };
    }

    private static CreatePolicyRuleRequest? BuildAutoTag(
        LibraryFolderPreset preset,
        LibraryFolderChoice choice,
        ProcessingCapabilities capabilities)
    {
        if (preset.AutoTagGroup is null || !choice.AutoTag || !capabilities.AutoTaggingActive) return null;

        // Source is ignored by the trigger today; FileMetadata matches what the rule editor sends.
        var parameters = new AutoTagParameters(TagSource.FileMetadata);

        return new CreatePolicyRuleRequest
        {
            ActionType = PolicyActionType.AutoTag,
            TriggerType = PolicyTriggerType.FileGroup,
            TriggerValue = preset.AutoTagGroup,
            Priority = AutoTagPriority,
            ApplyOnNewVersion = true,
            Parameters = JsonSerializer.SerializeToDocument(parameters, ParameterOptions),
        };
    }
}