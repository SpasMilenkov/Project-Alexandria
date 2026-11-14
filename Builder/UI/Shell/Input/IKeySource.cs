namespace Builder.UI.Shell.Input;

// Single input abstraction for the whole TUI. The real implementation wraps
// the console; tests feed synthetic sequences. Poll() exists so long-running
// phases (deploy) can keep the frame alive while still honoring T.
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
