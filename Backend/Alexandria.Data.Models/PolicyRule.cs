using System.Text.Json;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Data.Models;

public sealed class PolicyRule : IBase
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Guid PolicyId { get; set; }

    /// <summary>Discriminator column. Determines how Parameters is deserialized.</summary>
    public PolicyActionType ActionType { get; set; }

    public PolicyTriggerType TriggerType { get; set; }

    /// <summary>The value matched against the trigger, e.g. "mp3", "video/mp4".</summary>
    public string TriggerValue { get; set; } = null!;

    /// <summary>Lower number runs first when multiple rules match.</summary>
    public int Priority { get; set; }

    /// <summary>
    /// Opaque JSON storage for action-specific parameters.
    /// Always deserialized via PolicyRuleParametersResolver using ActionType as the discriminator.
    /// Never read as raw JSON outside of the resolver.
    /// </summary>
    public JsonDocument Parameters { get; set; } = null!;

    /// <summary>Whether the rule re-evaluates when a new file version is created.</summary>
    public bool ApplyOnNewVersion { get; set; }

    public DirectoryPolicy Policy { get; set; } = null!;
}