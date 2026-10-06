using Builder.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell;

// In-frame replacements for Spectre prompts, which cannot run inside the
// persistent Layout. Every control redraws the whole main pane on each
// keystroke through the shell, so the sidebar never stops being live.

public sealed class TextFieldSpec
{
    public required string Label { get; init; }
    public string Initial { get; init; } = string.Empty;
    public bool Secret { get; init; }
    public Func<string, string?>? Validator { get; init; } // null = valid, string = error
    public string? Hint { get; init; }
}

public static class Widgets
{
    private const string Cursor = "▏";

    public static string AskText(Shell shell, IRenderable header, TextFieldSpec spec)
    {
        var buffer = spec.Initial;
        var untouched = buffer.Length > 0; // first keystroke replaces the suggestion
        string? error = null;

        shell.GlobalsSuppressed = true;

        try
        {
            while (true)
            {
                var shown = spec.Secret ? new string('•', buffer.Length) : Markup.Escape(buffer);

                var body = new Rows(
                    header,
                    new Rule().RuleStyle(Theme.Active.BorderStyle),
                    new Markup($"[{Theme.Active.Ac}]▸[/] [bold]{Markup.Escape(spec.Label)}[/]"),
                    new Markup($"[{Theme.Active.Ac}]{shown}{Cursor}[/]" +
                               (spec.Hint is null ? string.Empty : $"  [dim]{Markup.Escape(spec.Hint)}[/]")),
                    ErrorLine(error));

                shell.SetMain(body);

                var key = shell.Keys.Wait();

                if (shell.HandleGlobalKey(key))
                {
                    continue;
                }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    error = spec.Validator?.Invoke(buffer);

                    if (error is null)
                    {
                        return buffer;
                    }

                    break;
                case ConsoleKey.Backspace when buffer.Length > 0:
                    buffer = buffer[..^1];
                    error = null;
                    break;
                case ConsoleKey.Escape:
                    shell.SignalEscape();

                    return buffer;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        buffer = untouched
                            ? key.KeyChar.ToString()
                            : buffer + key.KeyChar;

                        untouched = false;
                        error = null;
                    }

                    break;
            }
            }
        }
        finally
        {
            shell.GlobalsSuppressed = false;
        }
    }

    // y / n pressed directly; Enter accepts the default
    public static bool AskConfirm(Shell shell, IRenderable header, string question, bool defaultValue)
    {
        var current = defaultValue;

        while (true)
        {
            var body = new Rows(
                header,
                new Rule().RuleStyle(Theme.Active.BorderStyle),
                new Markup($"[bold]{Markup.Escape(question)}[/]"),
                new Markup(RenderToggle(current)));

            shell.SetMain(body);

            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key))
            {
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.Y:
                    current = true;
                    break;
                case ConsoleKey.N:
                    current = false;
                    break;
                case ConsoleKey.Escape:
                    shell.SignalEscape();

                    return current;
                case ConsoleKey.Enter:
                    return current;
            }
        }
    }

    // Returns null when the user pressed Esc - callers decide whether that
    // means "go back a screen" or "re-render this menu"
    public static T? AskSingleSelect<T>(
        Shell shell,
        IRenderable header,
        string title,
        IReadOnlyList<T> items,
        Func<T, string> convert,
        int initialIndex = 0) where T : class
    {
        var index = Math.Clamp(initialIndex, 0, items.Count - 1);

        while (true)
        {
            var list = new Grid().AddColumn();

            for (var i = 0; i < items.Count; i++)
            {
                list.AddRow(new Markup(i == index
                    ? $"[{Theme.Active.Ac}]▸ {convert(items[i])}[/]"
                    : $"[dim]  {Markup.Escape(convert(items[i]))}[/]"));
            }

            shell.SetMain(new Rows(
                header,
                new Rule().RuleStyle(Theme.Active.BorderStyle),
                new Markup($"[bold]{Markup.Escape(title)}[/]"),
                list));

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

    // Space toggles the highlighted entry; onChanged fires immediately so the
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
                var marker = isEnabled(items[i])
                    ? $"[{Theme.Active.Su}]●[/]"
                    : $"[dim]○[/]";

                var label = i == index
                    ? $"[{Theme.Active.Ac}]{marker} {Markup.Escape(convert(items[i]))}[/]"
                    : $"{marker} [dim]{Markup.Escape(convert(items[i]))}[/]";

                list.AddRow(new Markup(label));
            }

            var detail = detailFor?.Invoke(index);
            var parts = new List<IRenderable> { header };

            parts.Add(new Rule().RuleStyle(Theme.Active.BorderStyle));
            parts.Add(new Markup($"[bold]{Markup.Escape(title)}[/]"));
            parts.Add(list);

            if (detail is not null)
            {
                parts.Add(new Rule().RuleStyle(Theme.Active.BorderStyle));
                parts.Add(detail);
            }

            shell.SetMain(new Rows(parts.ToArray()));

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

    private static string RenderToggle(bool value)
    {
        var yes = value ? $"[bold {Theme.Active.Su}]Yes[/]" : "[dim]Yes[/]";
        var no = !value ? $"[bold {Theme.Active.Er}]No[/]" : "[dim]No[/]";

        return $"{yes}   {no}   [dim](Y/N, Enter keeps highlighted)[/]";
    }

    private static Markup ErrorLine(string? error)
    {
        return error is null
            ? new Markup(string.Empty)
            : new Markup($"[{Theme.Active.Er}]{Markup.Escape(error)}[/]");
    }
}
