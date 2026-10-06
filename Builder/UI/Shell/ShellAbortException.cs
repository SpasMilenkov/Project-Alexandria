namespace Builder.UI.Shell;

// Thrown when the user aborts via Ctrl+C inside the frame. RunAll catches it
// so unwinding passes through normal disposal - Environment.Exit must never
// appear here (it skips using-blocks and leaves the terminal mangled).
public sealed class ShellAbortException : Exception
{
    public static readonly ShellAbortException Instance = new();

    private ShellAbortException()
        : base("Installation aborted by user.")
    {
    }
}
