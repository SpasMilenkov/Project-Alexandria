using Alexandria.Common.Services;

namespace Alexandria.Tests.Common.Fakes;

/// <summary>Counts relay wake-ups so a test can tell whether a commit notified the relay.</summary>
public sealed class RecordingOutboxSignal : IOutboxSignal
{
    public int Notifications { get; private set; }

    public void Notify() => Notifications++;
}
