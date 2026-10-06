namespace Builder.UI.Shell;

// Single input abstraction for the whole TUI. Real implementation wraps the
// console; tests feed synthetic sequences. Poll() exists so long-running
// phases (deploy) can keep the frame alive while still honoring [T].
public interface IKeySource
{
    ConsoleKeyInfo Wait();

    ConsoleKeyInfo? Poll();
}

public sealed class ConsoleKeySource : IKeySource
{
    public static readonly ConsoleKeySource Instance = new();

    public ConsoleKeyInfo Wait() => Console.ReadKey(intercept: true);

    public ConsoleKeyInfo? Poll()
    {
        return Console.KeyAvailable ? Console.ReadKey(intercept: true) : null;
    }
}
