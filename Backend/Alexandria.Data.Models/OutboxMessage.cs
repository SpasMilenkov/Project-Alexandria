namespace Alexandria.Data.Models;

/// <summary>
/// A broker message saved in the same transaction as the change it announces, so the change and
/// the message are stored together or not at all. A relay sends it afterwards and marks it sent
/// once the broker confirms it.
/// </summary>
public class OutboxMessage : IBase
{
    public Guid Id { get; set; }

    public string RoutingKey { get; set; } = string.Empty;

    public byte[] Body { get; set; } = [];

    /// <summary>
    /// The job this message starts, if any. When the message dies the job is failed, so it does
    /// not wait in Queued for a message that will never arrive.
    /// </summary>
    public Guid? JobId { get; set; }

    /// <summary>The message is not sent before this time; it moves forward after a refused send.</summary>
    public DateTime AvailableAt { get; set; }

    /// <summary>Sends the broker refused. Lost connections are not counted.</summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime? SentAt { get; set; }

    /// <summary>Set once the relay gives up on the message; cleared when an admin resends it.</summary>
    public DateTime? DeadAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
