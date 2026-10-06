using Builder.UI.Shell;
using Spectre.Console;

namespace Builder.Tests.Support;

// Synthetic keyboard for shell flows: queue the exact key sequence a user
// would press, then run the screen.
public sealed class FakeKeys : IKeySource
{
    private readonly Queue<ConsoleKeyInfo> _keys = new();

    public FakeKeys Press(ConsoleKey key, char ch = '\0', bool ctrl = false)
    {
        _keys.Enqueue(new ConsoleKeyInfo(ch, key, false, false, ctrl));

        return this;
    }

    public FakeKeys Text(string text) // types characters without Enter
    {
        foreach (var ch in text.Where(c => !char.IsControl(c)))
        {
            _keys.Enqueue(new ConsoleKeyInfo(ch, MapKey(ch), false, false, false));
        }

        return this;
    }

    public FakeKeys Enter() => Press(ConsoleKey.Enter, '\r');

    public FakeKeys Space() => Press(ConsoleKey.Spacebar, ' ');

    public FakeKeys Backspace() => Press(ConsoleKey.Backspace, '\b');

    public FakeKeys Backspaces(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Backspace();
        }

        return this;
    }

    public FakeKeys Up() => Press(ConsoleKey.UpArrow);

    public FakeKeys Down() => Press(ConsoleKey.DownArrow);

    public FakeKeys Left() => Press(ConsoleKey.LeftArrow);

    public FakeKeys Right() => Press(ConsoleKey.RightArrow);

    public FakeKeys T() => Press(ConsoleKey.T, 't');

    private static ConsoleKey MapKey(char c) => c switch
    {
        >= 'a' and <= 'z' => ConsoleKey.A + (c - 'a'),
        >= 'A' and <= 'Z' => ConsoleKey.A + (c - 'A'),
        >= '0' and <= '9' => ConsoleKey.D0 + (c - '0'),
        '@' => ConsoleKey.D2,
        '.' => ConsoleKey.OemPeriod,
        '-' => ConsoleKey.OemMinus,
        '/' => ConsoleKey.Oem2,
        '~' => ConsoleKey.Oem3,
        _ => ConsoleKey.Oem1,
    };

    public ConsoleKeyInfo Wait()
    {
        if (_keys.Count == 0)
        {
            throw new InvalidOperationException(
                "FakeKeys ran dry - the screen asked for more input than the test scripted.");
        }

        return _keys.Dequeue();
    }

    public ConsoleKeyInfo? Poll() => _keys.Count > 0 ? DequeueSafe() : null;

    private ConsoleKeyInfo DequeueSafe()
    {
        var ok = _keys.TryDequeue(out var k);

        return ok ? k : new ConsoleKeyInfo('\0', ConsoleKey.Enter, false, false, false);
    }
}
