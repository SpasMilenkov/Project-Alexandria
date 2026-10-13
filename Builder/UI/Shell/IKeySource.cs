namespace Builder.UI.Shell;

// Single input abstraction for the whole TUI. Real implementation wraps the
// console; tests feed synthetic sequences. Poll() exists so long-running
// phases (deploy) can keep the frame alive while still honoring [T].
public interface IKeySource
{
    ConsoleKeyInfo Wait();

    ConsoleKeyInfo Wait(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Wait();
    }

    ConsoleKeyInfo? Poll();
}

public sealed class ConsoleKeySource : IKeySource
{
    public static readonly ConsoleKeySource Instance = new();

    public ConsoleKeyInfo Wait() => Console.ReadKey(intercept: true);

    public ConsoleKeyInfo Wait(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Poll();

            if (key.HasValue)
            {
                return key.Value;
            }

            cancellationToken.WaitHandle.WaitOne(50);
        }
    }

    public ConsoleKeyInfo? Poll()
    {
        return Console.KeyAvailable ? Console.ReadKey(intercept: true) : null;
    }
}

internal sealed class CancellableKeySource(IKeySource inner, CancellationToken cancellationToken) : IKeySource
{
    public ConsoleKeyInfo Wait() => inner.Wait(cancellationToken);

    public ConsoleKeyInfo? Poll()
    {
        cancellationToken.ThrowIfCancellationRequested();

        return inner.Poll();
    }
}
