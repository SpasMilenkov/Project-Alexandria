namespace Alexandria.Common.Settings;

/// <summary>Outbox relay tuning, bound from the <c>Outbox</c> configuration section.</summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// How often the relay looks for messages when nothing woke it. A commit in the same process
    /// wakes it at once, so this only paces messages left by other processes or by a crash.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 50;

    /// <summary>Refused sends before a message is marked dead. Lost connections do not count.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Wait after the first refused send; each further refusal doubles it.</summary>
    public int RetryBaseDelaySeconds { get; set; } = 5;

    public int RetryMaxDelaySeconds { get; set; } = 300;

    public int RetentionDays { get; set; } = 7;

    /// <summary>The health check fails once the oldest unsent message is older than this.</summary>
    public int BacklogAlertMinutes { get; set; } = 5;
}
