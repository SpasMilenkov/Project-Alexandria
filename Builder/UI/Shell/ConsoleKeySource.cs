namespace Builder.UI.Shell;

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
