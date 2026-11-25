namespace Alexandria.Common.Services;

/// <summary>Wakes this process's outbox relay so a new message goes out without waiting for the next poll.</summary>
public interface IOutboxSignal
{
    void Notify();
}
