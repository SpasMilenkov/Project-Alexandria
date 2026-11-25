using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Infrastructure.Messaging.Outbox;

/// <summary>
/// The process this relay runs in. Every publishing host runs its own relay, so dead-message
/// incidents name the service whose logs hold the failure.
/// </summary>
public sealed class OutboxHost
{
    public required ServiceType Service { get; init; }
}
