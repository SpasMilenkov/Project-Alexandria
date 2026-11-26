using Builder.UI.Shell.Input;

namespace Builder.Tests.Support;

// Keys reach only the screens that wait for one. Background work polls for global keys, which
// would otherwise swallow the input scripted for the page that follows it.
public sealed class WaitOnlyKeys(FakeKeys keys) : IKeySource
{
    public ConsoleKeyInfo Wait() => keys.Wait();

    public ConsoleKeyInfo? Poll() => null;
}
