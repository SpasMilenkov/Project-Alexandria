namespace Alexandria.Common.Services;

/// <summary>
/// Read-only view of the message broker connection, so services can skip or refuse
/// queue work up front instead of half-applying a change and failing on publish.
/// </summary>
public interface IMessageBrokerStatus
{
    /// <summary>False when the deployment turned the broker off (<c>RabbitMQ:Enabled=false</c>).</summary>
    bool IsEnabled { get; }

    /// <summary>True only while a broker connection is open.</summary>
    bool IsAvailable { get; }
}
