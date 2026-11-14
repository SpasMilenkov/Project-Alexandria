namespace Builder.UI.Shell.Input;

internal sealed class CancellableKeySource(IKeySource inner, CancellationToken cancellationToken) : IKeySource
{
    public ConsoleKeyInfo Wait() => inner.Wait(cancellationToken);

    public ConsoleKeyInfo? Poll()
    {
        cancellationToken.ThrowIfCancellationRequested();

        return inner.Poll();
    }
}
