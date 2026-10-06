using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using Builder.UI.Shell.Screens;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell;

public static class ShellRenderableExtensions
{
    // Layout.Update has an IRenderable overload only; strings are ubiquitous
    public static void Update(this Layout layout, string text)
    {
        layout.Update(new Markup(text));
    }
}

// The persistent frame. One Live session spans the entire wizard; screens
// swap the main pane and mutate the sidebar without ever tearing chrome down
// (locked decisions D32/D33).
//
// Global keys are handled before screens see anything:
//   T     - cycle palette, apply instantly, persist per press (D32)
//   Ctrl+C- graceful abort routed to the cleanup hook
public sealed class Shell : IDisposable
{
    public const int MinFullWidth = 80;
    public const int SidebarWidth = 40;
    private const int SidebarCompactWidth = 25;
    private IRenderable? _mainPanel;

    private readonly Layout _layout;
    private readonly IKeySource _keys;
    private LiveDisplayContext? _liveContext;
    private SidebarModel _sidebar = new();
    private IRenderable? _mainContent;

    public InstallationContext Context { get; }
    public IKeySource Keys => _keys;
    public Action? OnAbort { get; set; }

    // Test seam: redirects palette persistence (D32) to a scratch store
    internal Func<string, bool>? ThemeSaver { get; set; }

    public Shell(InstallationContext context, IKeySource? keys = null)
    {
        Context = context;
        _keys = keys ?? ConsoleKeySource.Instance;
        _layout = BuildLayout();
    }

    // Test seam: TestConsole reports interactive but buffers Live frames away
    // from Output - harnesses disable Live to assert rendered content
    public bool EnableLive { get; set; } = true;

    // While a widget captures raw text, printable keys (including T) must
    // reach the buffer - only Ctrl+C stays global
    public bool GlobalsSuppressed { get; set; }

    private bool UseLive => EnableLive && AnsiConsole.Profile.Capabilities.Interactive;

    public void RunAll(IReadOnlyList<(string Title, Func<Shell, ScreenOutcome> Screen)> screens)
    {
        if (!UseLive)
        {
            WalkScreensGuarded(screens);

            return;
        }

        // D39: alternate screen gives us the whole canvas - resizes cannot
        // corrupt scrollback and exit restores the user's terminal pristine
        var useAltBuffer = AnsiConsole.Profile.Capabilities.AlternateBuffer;

        try
        {
            if (useAltBuffer)
            {
                AnsiConsole.AlternateScreen(() => RunLive(screens));
            }
            else
            {
                RunLive(screens);
            }
        }
        catch (ShellAbortException)
        {
            // Graceful unwind; cleanup already ran through OnAbort
        }
        finally
        {
            StopFrameTimer();
            _liveContext = null;
        }
    }

    private void RunLive(IReadOnlyList<(string Title, Func<Shell, ScreenOutcome> Screen)> screens)
    {
        StartFrameTimer();

        // Crop/Top keeps an oversized frame from spilling into alt-buffer
        // scrollback - the freshest rows stay visible, nothing scrolls
        AnsiConsole.Live(_layout)
            .AutoClear(true)
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Top)
            .Start(liveContext =>
            {
                _liveContext = liveContext;

                try
                {
                    WalkScreens(screens);
                }
                catch (ShellAbortException)
                {
                    // unwinding only
                }
            });
    }

    private void WalkScreensGuarded(IReadOnlyList<(string Title, Func<Shell, ScreenOutcome> Screen)> screens)
    {
        try
        {
            WalkScreens(screens);
        }
        catch (ShellAbortException)
        {
        }
    }

    private CancellationTokenSource? _frameTimerCts;
    private int _lastWidth = -1;
    private int _lastHeight = -1;

    // Live only paints on explicit Refresh() (unlike Progress/Status, there is
    // no internal refresher), and ReadKey never sees resizes - this timer is
    // the single repaint heartbeat. Screens stay stateless: animated glyphs
    // derive from wall-clock time, not from ticks raised here.
    private void StartFrameTimer()
    {
        if (_frameTimerCts is not null) return;

        _frameTimerCts = new CancellationTokenSource();

        var token = _frameTimerCts.Token;

        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(120));

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    CheckResize();
                    Refresh();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void StopFrameTimer()
    {
        _frameTimerCts?.Cancel();
        _frameTimerCts?.Dispose();
        _frameTimerCts = null;
    }

    // D40: resizes are invisible to a blocking ReadKey loop - the heartbeat
    // notices dimension changes and triggers clear + full recompose
    private bool CheckResize()
    {
        int width, height;

        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight;
        }
        catch
        {
            return false; // redirected output has no window metrics
        }

        if (_lastWidth == -1)
        {
            _lastWidth = width;
            _lastHeight = height;

            return false;
        }

        if (width == _lastWidth && height == _lastHeight)
        {
            return false;
        }

        _lastWidth = width;
        _lastHeight = height;

        AnsiConsole.Clear();
        ComposeFrame();

        return true;
    }

    // Back steps to the previous screen (D38); at the first screen it is a
    // no-op re-render. Abort leaves the walk immediately.
    private void WalkScreens(IReadOnlyList<(string Title, Func<Shell, ScreenOutcome> Screen)> screens)
    {
        var total = screens.Count;

        for (var i = 0; i < total;)
        {
            var (title, screen) = screens[i];
            StepIndex = i + 1;
            TotalSteps = total;
            ScreenTitle = title;

            RefreshSidebarModel();
            SetMain(new Markup(string.Empty));

            var outcome = screen(this);

            switch (outcome)
            {
                case ScreenOutcome.Abort:
                    return;
                case ScreenOutcome.Back when i > 0:
                    i -= 1;
                    break;
                default:
                    i += 1;
                    break;
            }
        }
    }

    public int StepIndex { get; private set; }

    public int TotalSteps { get; private set; }

    public string ScreenTitle { get; private set; } = string.Empty;

    public void SetMain(IRenderable renderable)
    {
        _mainContent = renderable;
        ComposeFrame();

        if (UseLive)
        {
            _liveContext?.Refresh();
        }
        else if (_mainPanel is not null)
        {
            // Dry mode: emit the composed frame so output is observable
            AnsiConsole.Write(_mainPanel);
        }
    }

    public void SetSidebar(SidebarModel model)
    {
        _sidebar = model;
        Refresh();
    }

    public void UpdateSidebarStats(Action<List<(string Label, string Value)>> mutate)
    {
        mutate(_sidebar.Stats);
        Refresh();
    }

    // Returns true when the key was consumed as a global command
    public bool HandleGlobalKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            AbortRequested = true;
            OnAbort?.Invoke();
            throw ShellAbortException.Instance;
        }

        if (GlobalsSuppressed)
        {
            return false;
        }

        if (key.Key == ConsoleKey.T)
        {
            CycleTheme();

            return true;
        }

        return false;
    }

    public bool AbortRequested { get; private set; }

    private bool _escapeRequested;

    // Widgets signal Esc; screens consume it to step back one screen (D38)
    public void SignalEscape() => _escapeRequested = true;

    public bool TakeEscape()
    {
        var taken = _escapeRequested;

        _escapeRequested = false;

        return taken;
    }

    internal SidebarModel CurrentSidebar => _sidebar;

    private void CycleTheme()
    {
        var all = Themes.All;
        var nextIndex = 0;

        for (var i = 0; i < all.Count; i++)
        {
            if (ReferenceEquals(all[i], Theme.Active))
            {
                nextIndex = (i + 1) % all.Count;
                break;
            }
        }

        var next = all[nextIndex];

        Theme.Use(next);

        var saved = ThemeSaver?.Invoke(next.Id) ?? UiPreferencesStore.Save(next.Id);

        _sidebar.SaveWarning = saved
            ? null
            : $"Look not saved - resets on next start";

        Refresh();
    }

    private Layout BuildLayout()
    {
        // Single region: the whole frame is composed as one renderable and
        // centered inside it - Layout is just the Live carrier now
        return new Layout("root");
    }

    private bool IsNarrow()
    {
        return AnsiConsole.Profile.Width < MinFullWidth;
    }

    public void Refresh()
    {
        ComposeFrame();
        _liveContext?.Refresh();
    }

    private void RefreshSidebarModel()
    {
        _sidebar.StepLabel = TotalSteps > 0
            ? $"Step {StepIndex} / {TotalSteps} - {ScreenTitle}"
            : ScreenTitle;

        ComposeFrame();
    }

    // Content measure cap: on wide terminals the main pane's inner block is
    // centered and capped so text never stretches across the whole panel
    internal const int MaxContentMeasure = 78;

    private const int HorizontalBreathing = 3;

    // D52: constant content inset - identical column on every screen
    internal const int MainContentInset = 4;

    // Full-width chrome; the main pane's inner content is measure-capped and
    // centered (D41 rev.2). Below MinFullWidth the sidebar column is dropped
    // entirely - no ghost gap.
    private void ComposeFrame()
    {
        var width = AnsiConsole.Profile.Width;
        var totalRows = Math.Max(6, AnsiConsole.Profile.Height);
        var narrow = IsNarrow();

        var sidebarWidth = narrow ? SidebarCompactWidth : SidebarWidth;
        var mainColWidth = narrow ? width : width - sidebarWidth - 1; // Columns spacing

        var stepBar = BuildStepCard();
        var sidebarInner = narrow ? BuildCompactSidebar() : BuildSidebarContent();

        var mainInnerWidth = Math.Max(8, mainColWidth - 2 /* borders */ - 2 * HorizontalBreathing);

        var (mainContent, _, _) = FixedInset(
            PadHorizontal(_mainContent ?? new Markup(string.Empty), 1),
            mainInnerWidth,
            MainContentInset,
            MaxContentMeasure);

        var mainPanel = BuildFramedPanel(
            titleMarkup: $"{stepBar}  [bold white]{Markup.Escape(ScreenTitle)}[/]",
            content: mainContent,
            columnWidth: mainColWidth,
            totalRows: totalRows,
            horizontalPadding: HorizontalBreathing);

        IRenderable body;

        if (narrow)
        {
            // D41 rev.2: below full width the sidebar is dropped entirely -
            // the main pane takes the whole terminal, no ghost gap
            body = mainPanel;
        }
        else
        {
            var sidebarPanel = BuildFramedPanel(
                titleMarkup: $"[{Theme.Active.Ac}]{Theme.Active.DisplayName}[/] [dim]look[/]",
                content: PadHorizontal(sidebarInner, 1),
                columnWidth: sidebarWidth,
                totalRows: totalRows,
                horizontalPadding: 1);

            body = new Columns(mainPanel, sidebarPanel).Collapse();
        }

        _layout.Update(new Rows(new Markup(string.Empty), body));
        _mainPanel = mainPanel;

        // Pure layout update - repaint happens exclusively through Refresh(),
        // never here (ComposeFrame -> Refresh -> ComposeFrame recursion bug)
    }

    private string BuildStepCard()
    {
        if (TotalSteps == 0)
        {
            return string.Empty;
        }

        const int cells = 9;
        var filled = Math.Min(cells, (int)Math.Ceiling((double)StepIndex / TotalSteps * cells));
        var bar = $"[bold {Theme.Active.Ac}]{new string('█', filled)}[/][dim]{new string('░', cells - filled)}[/]";

        return $"{bar} [dim]{StepIndex}/{TotalSteps}[/]";
    }

    // D52: content starts at a constant inset derived from nothing but the
    // geometry knobs - identical on every screen, regardless of what the
    // screen renders. The right padding enforces MaxContentMeasure.
    internal static (IRenderable Renderable, int LeftPad, int RightPad) FixedInset(
        IRenderable content,
        int availableWidth,
        int inset,
        int maxMeasure)
    {
        var rightPad = Math.Max(0, availableWidth - inset - maxMeasure);
        var renderable = new Padder(content, new Padding(inset, 0, rightPad, 0));

        return (renderable, inset, rightPad);
    }

    private static IRenderable PadHorizontal(IRenderable content, int pad)
    {
        return new Padder(content, new Padding(pad, 0, pad, 0));
    }

    private static IRenderable[] BlankLines(int count)
    {
        var blanks = new IRenderable[count];

        for (var i = 0; i < count; i++)
        {
            blanks[i] = new Markup(" ");
        }

        return blanks;
    }

    private IRenderable BuildFramedPanel(
        string titleMarkup,
        IRenderable content,
        int columnWidth,
        int totalRows,
        int horizontalPadding = 2)
    {
        const int borderRows = 2;
        var horizontalChrome = 2 /* borders */ + 2 * horizontalPadding;

        var innerWidth = Math.Max(8, columnWidth - horizontalChrome);
        var innerRows = Math.Max(3, totalRows - borderRows);
        var contentRows = MeasureLines(content, innerWidth);

        var parts = new List<IRenderable> { content };

        if (contentRows < innerRows)
        {
            parts.AddRange(BlankLines(innerRows - contentRows));
        }

        return new Panel(new Rows(parts.ToArray()))
        {
            Header = new PanelHeader(titleMarkup),
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.Active.BorderStyle,
            Padding = new Padding(horizontalPadding, 0, horizontalPadding, 0),
            Expand = true,
            Width = columnWidth,
        };
    }

    // Renders to a throwaway console to count wrapped output lines
    private static int MeasureLines(IRenderable renderable, int width)
    {
        var text = RenderToString(renderable, width);
        var lines = 0;

        foreach (var _ in text.Split('\n'))
        {
            lines++;
        }

        return lines;
    }

    private static string RenderToString(IRenderable renderable, int width)
    {
        var writer = new StringWriter();

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
        });

        console.Profile.Width = width;
        console.Profile.Height = 1000;
        console.Profile.Capabilities.Interactive = false;
        console.Write(renderable);

        return writer.ToString();
    }

    private IRenderable BuildCompactSidebar()
    {
        return new Rows(
            new Markup($"[{Theme.Active.Ac}]{Theme.Active.DisplayName}[/]"),
            new Markup($"[dim]{_sidebar.StepLabel}[/]"),
            new Markup("[dim]T look[/]"));
    }

    // Inner sidebar content; the framed panel around it is built by
    // ComposeFrame so both panes share identical height and chrome.
    // D47: per-feature dots appear once features have been selected.
    internal IRenderable BuildSidebarContent()
    {
        var rows = new List<IRenderable>
        {
            new Markup($"[{Theme.Active.Ac}]{Theme.Active.DisplayName}[/] [dim]look[/]"),
            new Markup($"[dim]{new string('█', 8)}[/] [dim]([/]T[dim] switch)[/]"),
            new Rule().RuleStyle(Theme.Active.BorderStyle),
            new Markup($"[dim]{_sidebar.StepLabel}[/]"),
            new Rule().RuleStyle(Theme.Active.BorderStyle),
            BuildStats(),
        };

        if (Context.FeaturesSelected)
        {
            rows.Add(new Rule().RuleStyle(Theme.Active.BorderStyle));
            rows.Add(new Markup("[dim]Features[/]"));

            foreach (var feature in FeatureCatalog.All)
            {
                var on = Context.Features.IsEnabled(feature.Id);
                var dot = on ? $"[{Theme.Active.Su}]●[/]" : "[dim]○[/]";
                var label = TruncateLabel(feature.Label, 24);

                rows.Add(new Markup(on
                    ? $"{dot} {Markup.Escape(label)}"
                    : $"{dot} [dim]{Markup.Escape(label)}[/]"));
            }
        }

        if (_sidebar.SaveWarning is not null)
        {
            rows.Add(new Markup($"[{Theme.Active.Wa}]⚠ {_sidebar.SaveWarning}[/]"));
        }

        rows.Add(new Rule().RuleStyle(Theme.Active.BorderStyle));
        rows.Add(new Markup(string.Join("\n", SidebarModel.Legend.Select(l => $"[dim]{l}[/]"))));

        return new Rows(rows.ToArray());
    }

    private static string TruncateLabel(string label, int max)
    {
        return label.Length <= max ? label : label[..(max - 1)] + "…";
    }

    private IRenderable BuildStats()
    {
        if (_sidebar.Stats.Count == 0)
        {
            return new Markup("[dim]Nothing measured yet[/]");
        }

        var grid = new Grid().AddColumn().AddColumn();

        foreach (var (label, value) in _sidebar.Stats)
        {
            grid.AddRow(new Markup($"[dim]{label}[/]"), new Markup(value));
        }

        return grid;
    }

    public void Dispose()
    {
        // Live session disposes through its own scope; nothing owned here yet
    }
}
