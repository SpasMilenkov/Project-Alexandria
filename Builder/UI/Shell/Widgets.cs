using Builder.UI.Shell.Screens;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell;

// In-frame replacements for Spectre prompts, which cannot run inside the
// persistent Live frame. Every control redraws the whole main pane on each
// keystroke through the shell, so the sidebar never stops being live.
public static class Widgets
{
    private static readonly KeyHint[] TextHints =
    [
        new("Enter", "accept"),
        new("Esc", "back"),
        new("Ctrl+U", "clear"),
    ];

    private static readonly KeyHint[] SelectHints =
    [
        new("↑↓", "move"),
        new("Enter", "select"),
        new("Esc", "back"),
    ];

    private static readonly KeyHint[] ToggleHints =
    [
        new("↑↓", "move"),
        new("Space", "toggle"),
        new("Enter", "confirm"),
        new("Esc", "back"),
    ];

    public static string AskText(Shell shell, IRenderable header, TextFieldSpec spec)
    {
        var editor = new TextEditor(spec.Initial, spec.Secret);
        string? error = null;
        var previousSuppression = shell.GlobalsSuppressed;

        // Printable keys, T included, belong to the text being typed
        shell.GlobalsSuppressed = true;

        try
        {
            while (true)
            {
                var rows = new List<IRenderable>
                {
                    header,
                    new Markup($"[bold]{Markup.Escape(spec.Label)}[/]"),
                    new Panel(editor)
                    {
                        Border = BoxBorder.Rounded,
                        BorderStyle = error is null ? Theme.Active.AccentStyle : Theme.Active.ErrorStyle,
                        Padding = new Padding(1, 0),
                        Expand = true,
                    },
                };

                if (error is not null)
                {
                    rows.Add(new Markup($"[{Theme.Active.Er}]✗ {Markup.Escape(error)}[/]"));
                }

                if (spec.Hint is not null)
                {
                    rows.Add(Views.Note(spec.Hint));
                }

                if (spec.Preview?.Invoke(editor.Value) is { } preview)
                {
                    rows.Add(Views.Blank());
                    rows.Add(preview);
                }

                shell.SetMain(new Rows(rows), TextHints);

                var key = shell.Keys.Wait();

                if (shell.HandleGlobalKey(key))
                {
                    continue;
                }

                if (editor.HandleKey(key))
                {
                    error = null;
                }

                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        error = spec.Validator?.Invoke(editor.Value);

                        if (error is null)
                        {
                            return editor.Value;
                        }

                        break;
                    case ConsoleKey.Escape:
                        shell.SignalEscape();

                        return editor.Value;
                }
            }
        }
        finally
        {
            shell.GlobalsSuppressed = previousSuppression;
        }
    }

    // Returns null when the user pressed Esc; callers decide whether that
    // means "go back a screen" or "re-render this menu". The detail renderable
    // describes the highlighted item below the list.
    public static T? AskSingleSelect<T>(
        Shell shell,
        IRenderable header,
        string title,
        IReadOnlyList<T> items,
        Func<T, string> convert,
        int initialIndex = 0,
        Func<T, IRenderable?>? detailFor = null) where T : class
    {
        var index = Math.Clamp(initialIndex, 0, items.Count - 1);

        while (true)
        {
            var list = new Grid().AddColumn();

            for (var i = 0; i < items.Count; i++)
            {
                list.AddRow(new Markup(ChoiceLine(convert(items[i]), i == index, marker: null, enabled: true)));
            }

            shell.SetMain(Compose(header, title, list, detailFor?.Invoke(items[index])), SelectHints);

            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key))
            {
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    index = (index - 1 + items.Count) % items.Count;
                    break;
                case ConsoleKey.DownArrow:
                    index = (index + 1) % items.Count;
                    break;
                case ConsoleKey.Escape:
                    shell.SignalEscape();

                    return null;
                case ConsoleKey.Enter:
                    return items[index];
            }
        }
    }

    // Space toggles the highlighted entry; setEnabled fires immediately so the
    // caller can push fresh stats into the sidebar between keystrokes.
    public static IReadOnlyList<T> AskMultiToggle<T>(
        Shell shell,
        IRenderable header,
        string title,
        IReadOnlyList<T> items,
        Func<T, bool> isEnabled,
        Action<T, bool> setEnabled,
        Func<T, string> convert,
        Func<int, IRenderable?>? detailFor = null)
    {
        var index = 0;

        while (true)
        {
            var list = new Grid().AddColumn();

            for (var i = 0; i < items.Count; i++)
            {
                var enabled = isEnabled(items[i]);
                var marker = enabled ? Ui.Success("●") : Ui.Muted("○");

                list.AddRow(new Markup(ChoiceLine(convert(items[i]), i == index, marker, enabled)));
            }

            shell.SetMain(Compose(header, title, list, detailFor?.Invoke(index)), ToggleHints);

            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key))
            {
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    index = (index - 1 + items.Count) % items.Count;
                    break;
                case ConsoleKey.DownArrow:
                    index = (index + 1) % items.Count;
                    break;
                case ConsoleKey.Spacebar:
                    setEnabled(items[index], !isEnabled(items[index]));
                    break;
                case ConsoleKey.Escape:
                    shell.SignalEscape();

                    return items.Where(isEnabled).ToList();
                case ConsoleKey.Enter:
                    return items.Where(isEnabled).ToList();
            }
        }
    }

    // Shows a page until Enter (returns false) or Esc (returns true, after
    // signalling the escape so screens can step back)
    public static bool WaitForEnter(Shell shell)
    {
        while (true)
        {
            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key))
            {
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    return false;
                case ConsoleKey.Escape:
                    shell.SignalEscape();

                    return true;
            }
        }
    }

    private static string ChoiceLine(string label, bool highlighted, string? marker, bool enabled)
    {
        var prefix = marker is null ? string.Empty : marker + " ";

        if (highlighted)
        {
            return $"[bold {Theme.Active.Ac}]❯[/] {prefix}[bold {Theme.Active.Ac}]{Markup.Escape(label)}[/]";
        }

        return enabled
            ? $"  {prefix}{Markup.Escape(label)}"
            : $"  {prefix}{Ui.Muted(label)}";
    }

    private static IRenderable Compose(IRenderable header, string title, IRenderable list, IRenderable? detail)
    {
        var rows = new List<IRenderable>
        {
            header,
            new Markup($"[bold]{Markup.Escape(title)}[/]"),
            list,
        };

        if (detail is not null)
        {
            rows.Add(Views.Blank());
            rows.Add(detail);
        }

        return new Rows(rows);
    }
}
