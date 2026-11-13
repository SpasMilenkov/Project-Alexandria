using Builder.Services;
using Builder.Workflow;
using Builder.UI.Shell.Screens;
using Builder.UI.Shell.Splash;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell;

// The persistent frame. One Live session spans the entire wizard; screens
// swap the main pane and mutate the sidebar without ever tearing chrome down.
//
// Global keys are handled before screens see anything:
//   T          cycle the look, apply instantly, persist per press
//   PgUp/PgDn  scroll the main pane when its content is taller than the window
//   Ctrl+C     graceful abort routed to the cleanup hook
public sealed class Shell : IDisposable
{
    public const int MinFullWidth = 100;
    public const int SidebarWidth = 34;

    // On wide terminals the main pane's text is capped to a readable measure
    internal const int MaxContentMeasure = 78;

    // Constant content inset, so every screen starts in the same column
    internal const int MainContentInset = 2;

    private const int MainPadding = 2;
    private const int SidebarPadding = 1;

    // How long the splash takes to fold away after a full build
    private const double SplashUnwindSeconds = 0.7;

    private static readonly KeyHint[] DefaultHints = [new("Enter", "continue"), new("Esc", "back")];

    private readonly object _frameGate = new();
    private readonly Layout _layout = new("root");
    private readonly IKeySource _keys;
    private LiveDisplayContext? _liveContext;
    private SidebarModel _sidebar = new();
    private IRenderable _mainContent = new Text(string.Empty);
    private Func<IRenderable>? _mainBuilder;
    private IReadOnlyList<KeyHint> _hints = DefaultHints;
    private IRenderable? _mainPanel;
    private bool _readFromTop;
    private int _scroll;
    private int _scrollPage;
    private bool _mainOverflows;
    private bool _escapeRequested;
    private CancellationTokenSource? _frameTimerCts;
    private Task? _frameTask;
    private int _lastWidth = -1;
    private int _lastHeight = -1;

    public Shell(InstallationContext context, IKeySource? keys = null)
    {
        Context = context;
        _keys = new CancellableKeySource(keys ?? ConsoleKeySource.Instance, context.Cancellation.Token);
    }

    public InstallationContext Context { get; }

    public IKeySource Keys => _keys;

    public Action? OnAbort { get; set; }

    // Test seam: redirects look persistence to a scratch store
    internal Func<string, bool>? ThemeSaver { get; set; }

    // Test seam: TestConsole reports interactive but buffers Live frames away
    // from Output; harnesses disable Live to assert rendered content
    public bool EnableLive { get; set; } = true;

    // Plays the opening city before the first screen; off for repair runs
    // and tests, which should land on the wizard straight away
    public bool ShowSplash { get; init; }

    // While a widget captures raw text, printable keys (including T) must
    // reach the buffer; only Ctrl+C stays global
    public bool GlobalsSuppressed { get; set; }

    public bool AbortRequested { get; private set; }

    public int StepIndex { get; private set; }

    public int TotalSteps { get; private set; }

    public string ScreenTitle { get; private set; } = string.Empty;

    internal IReadOnlyList<string> StepTitles { get; set; } = [];

    internal SidebarModel CurrentSidebar => _sidebar;

    // Width the main pane gives its content, for screens that wrap text
    // themselves (log viewers)
    public static int MainContentWidth
    {
        get
        {
            var width = AnsiConsole.Profile.Width;
            var column = width < MinFullWidth ? width : width - SidebarWidth - 1;

            return Math.Max(12, Math.Min(MaxContentMeasure, column - 2 - 2 * MainPadding - MainContentInset));
        }
    }

    private bool UseLive => EnableLive && AnsiConsole.Profile.Capabilities.Interactive;

    public void RunAll(IReadOnlyList<(string Title, Func<Shell, ScreenOutcome> Screen)> screens)
    {
        StepTitles = screens.Select(screen => screen.Title).ToArray();

        try
        {
            if (!UseLive)
            {
                WalkScreens(screens);
            }
            else if (AnsiConsole.Profile.Capabilities.AlternateBuffer)
            {
                // The alternate screen gives the frame the whole canvas: resizes
                // cannot corrupt scrollback and exit restores the terminal
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
        catch (OperationCanceledException) when (Context.Cancellation.IsCancellationRequested)
        {
            Context.ShouldAbort = true;
        }
        finally
        {
            StopFrameTimer();
            lock (_frameGate) _liveContext = null;
        }
    }

    private void RunLive(IReadOnlyList<(string Title, Func<Shell, ScreenOutcome> Screen)> screens)
    {
        // The frame is sized to the window, but a resize between measurement
        // and paint can still overflow; cropping the top keeps the key bar
        AnsiConsole.Live(_layout)
            .AutoClear(true)
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Top)
            .Start(liveContext =>
            {
                lock (_frameGate) _liveContext = liveContext;

                try
                {
                    PlaySplash(liveContext);
                    StartFrameTimer();
                    WalkScreens(screens);
                }
                catch (ShellAbortException)
                {
                    // Leave Live normally so it restores the terminal itself
                }
                finally
                {
                    StopFrameTimer();
                }
            });
    }

    // The splash owns the whole window until a key is pressed. A key then
    // plays the build backwards, quicker than it was built, from wherever it
    // had got to, so the map folds away into the wizard instead of being cut
    // off. A second key skips the rest. It is left out where it could not
    // look right: small, colorless or non-Unicode terminals.
    private void PlaySplash(LiveDisplayContext liveContext)
    {
        if (!ShowSplash || !SplashFits()) return;

        var clock = Stopwatch.StartNew();
        var painted = (Width: 0, Height: 0, Settled: false);

        while (SplashFits())
        {
            var elapsed = clock.Elapsed.TotalSeconds;
            var frame = (Width: AnsiConsole.Profile.Width, Height: AnsiConsole.Profile.Height, Settled: elapsed >= CityScene.Duration);

            // A settled scene is repainted only when the window changes size
            if (frame != painted)
            {
                PaintSplash(liveContext, elapsed);
                painted = frame.Settled ? frame : default;
            }

            if (Keys.Poll() is { } key)
            {
                AbortOnCtrlC(key);
                UnwindSplash(liveContext, Math.Min(elapsed, CityScene.Duration));

                return;
            }

            Thread.Sleep(elapsed < CityScene.Duration ? 33 : 120);
        }
    }

    private void UnwindSplash(LiveDisplayContext liveContext, double from)
    {
        var duration = SplashUnwindSeconds * from / CityScene.Duration;
        var clock = Stopwatch.StartNew();

        while (SplashFits())
        {
            var progress = Math.Min(1, clock.Elapsed.TotalSeconds / Math.Max(0.001, duration));

            // Eased at both ends so the fold starts and lands gently
            var eased = progress * progress * (3 - 2 * progress);

            PaintSplash(liveContext, from * (1 - eased));

            if (progress >= 1) return;

            if (Keys.Poll() is { } key)
            {
                AbortOnCtrlC(key);

                return;
            }

            Thread.Sleep(33);
        }
    }

    private void PaintSplash(LiveDisplayContext liveContext, double elapsed)
    {
        ClearOnResize();
        _layout.Update(new Rows(
            new CityScene { Height = AnsiConsole.Profile.Height - 2, Splash = true, Elapsed = elapsed },
            SplashPrompt(elapsed)));
        liveContext.Refresh();
    }

    // Ctrl+C keeps its meaning during the splash: it aborts and never returns
    private void AbortOnCtrlC(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            HandleGlobalKey(key);
        }
    }

    private static bool SplashFits()
    {
        var profile = AnsiConsole.Profile;

        return profile.Capabilities.Unicode &&
               profile.Capabilities.ColorSystem != ColorSystem.NoColors &&
               profile.Width >= CityPlan.MinSplashWidth &&
               profile.Height - 2 >= CityPlan.MinSplashHeight;
    }

    private static IRenderable SplashPrompt(double elapsed)
    {
        if (elapsed < CityScene.Duration) return new Text(string.Empty);

        return new Markup($"{Ui.Muted("press")} [bold {Theme.Active.Ac}]any key[/] {Ui.Muted("to begin")}").Centered();
    }

    // Back steps to the previous screen; at the first screen it re-renders.
    // Abort leaves the walk immediately.
    private void WalkScreens(IReadOnlyList<(string Title, Func<Shell, ScreenOutcome> Screen)> screens)
    {
        TotalSteps = screens.Count;

        for (var i = 0; i < screens.Count;)
        {
            var (title, screen) = screens[i];

            lock (_frameGate)
            {
                StepIndex = i + 1;
                ScreenTitle = title;
                _scroll = 0;
            }

            // An Esc a screen left unconsumed must not send the next one back
            _escapeRequested = false;

            SetMain(new Text(string.Empty));

            switch (screen(this))
            {
                case ScreenOutcome.Abort:
                    Context.ShouldAbort = true;
                    return;
                case ScreenOutcome.Back when i > 0:
                    i -= 1;
                    break;
                default:
                    i += 1;
                    break;
            }
        }

        Context.Completed = true;
    }

    // Replaces the main pane. Hints describe the keys the screen listens to;
    // null means the usual Enter/Esc pair. Content taller than the window is
    // anchored to its end (where inputs live) unless readFromTop is set.
    public void SetMain(IRenderable renderable, IReadOnlyList<KeyHint>? hints = null, bool readFromTop = false)
    {
        SetMain(() => renderable, hints, readFromTop);
    }

    // Builder form: the page is rebuilt when the look changes, so colors baked
    // into its markup follow the new theme while the screen waits for a key
    public void SetMain(Func<IRenderable> build, IReadOnlyList<KeyHint>? hints = null, bool readFromTop = false)
    {
        lock (_frameGate)
        {
            if (readFromTop != _readFromTop)
            {
                _scroll = 0;
            }

            _mainBuilder = build;
            _mainContent = build();
            _hints = hints ?? DefaultHints;
            _readFromTop = readFromTop;
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
    }

    public void SetSidebar(SidebarModel model)
    {
        lock (_frameGate)
        {
            _sidebar = model;
            Refresh();
        }
    }

    public void UpdateSidebarStats(Action<List<(string Label, string Value)>> mutate)
    {
        lock (_frameGate)
        {
            mutate(_sidebar.Stats);
            Refresh();
        }
    }

    public void Refresh()
    {
        lock (_frameGate)
        {
            ComposeFrame();
            _liveContext?.Refresh();
        }
    }

    // Returns true when the key was consumed as a global command; the caller
    // then redraws its page so the change shows
    public bool HandleGlobalKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            AbortRequested = true;
            Context.ShouldAbort = true;
            Context.Cancellation.Cancel();
            OnAbort?.Invoke();
            throw ShellAbortException.Instance;
        }

        if (_mainOverflows && key.Key is ConsoleKey.PageUp or ConsoleKey.PageDown)
        {
            Scroll(key.Key == ConsoleKey.PageDown);

            return true;
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

    public T RunResponsive<T>(Func<T> operation, Action? onTick = null)
    {
        var work = Task.Run(operation);
        ExceptionDispatchInfo? inputFailure = null;

        try
        {
            while (!work.IsCompleted)
            {
                var key = Keys.Poll();

                if (key.HasValue) HandleGlobalKey(key.Value);

                onTick?.Invoke();
                Task.WaitAny([work], 50);
            }
        }
        catch (Exception error)
        {
            inputFailure = ExceptionDispatchInfo.Capture(error);
            Context.Cancellation.Cancel();
        }

        T result = default!;

        try
        {
            result = work.GetAwaiter().GetResult();
        }
        catch when (inputFailure is not null)
        {
            // Drain the worker before the input exception unwinds the terminal.
        }

        inputFailure?.Throw();
        Context.Cancellation.Token.ThrowIfCancellationRequested();
        onTick?.Invoke();

        return result;
    }

    // Widgets signal Esc; screens consume it to step back one screen
    public void SignalEscape() => _escapeRequested = true;

    public bool TakeEscape()
    {
        var taken = _escapeRequested;

        _escapeRequested = false;

        return taken;
    }

    // Content starts at a constant inset derived from the geometry alone, so
    // it is identical on every screen. The right padding enforces the measure.
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

    private void Scroll(bool forward)
    {
        lock (_frameGate)
        {
            var step = Math.Max(1, _scrollPage - 1);

            // The offset counts away from the anchor, so "forward" means
            // toward it only when reading from the bottom
            _scroll += forward == _readFromTop ? step : -step;
            _scroll = Math.Max(0, _scroll);
            Refresh();
        }
    }

    private void CycleTheme()
    {
        lock (_frameGate)
        {
            var next = Themes.After(Theme.Active);

            Theme.Use(next);

            var saved = ThemeSaver?.Invoke(next.Id) ?? UiPreferencesStore.Save(next.Id);

            _sidebar.SaveWarning = saved ? null : "Look not saved; resets on next start";

            if (_mainBuilder is not null)
            {
                _mainContent = _mainBuilder();
            }

            Refresh();
        }
    }

    // Live only paints on explicit Refresh() and ReadKey never sees resizes;
    // this timer is the single repaint heartbeat. Screens stay stateless:
    // animated glyphs derive from wall-clock time, not from ticks raised here.
    private void StartFrameTimer()
    {
        if (_frameTimerCts is not null) return;

        _frameTimerCts = new CancellationTokenSource();

        var token = _frameTimerCts.Token;

        _frameTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(120));

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    lock (_frameGate)
                    {
                        ClearOnResize();
                        Refresh();
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // The timer stops when the frame closes
            }
        }, token);
    }

    private void StopFrameTimer()
    {
        var cancellation = _frameTimerCts;
        var frameTask = _frameTask;
        _frameTimerCts = null;
        _frameTask = null;
        cancellation?.Cancel();

        try
        {
            frameTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Cancelled before the loop started; nothing was painted
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    // Resizes are invisible to a blocking ReadKey loop; the heartbeat notices
    // dimension changes and clears so the next paint is a full recompose
    private void ClearOnResize()
    {
        int width, height;

        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight;
        }
        catch (Exception error) when (error is IOException or PlatformNotSupportedException or InvalidOperationException)
        {
            // Redirected output has no window metrics to compare
            return;
        }

        if (width == _lastWidth && height == _lastHeight)
        {
            return;
        }

        var first = _lastWidth == -1;

        _lastWidth = width;
        _lastHeight = height;

        if (!first)
        {
            AnsiConsole.Clear();
        }
    }

    // Pure layout update; repainting happens only through Refresh() or
    // SetMain(), never here
    private void ComposeFrame()
    {
        var width = AnsiConsole.Profile.Width;
        // One row stays blank above the frame so the last frame row never
        // lands on the terminal's final line, where a newline would scroll it
        var rows = Math.Max(8, AnsiConsole.Profile.Height - 1);
        var narrow = width < MinFullWidth;
        var mainWidth = narrow ? width : width - SidebarWidth - 1;

        var mainPanel = BuildMainPanel(mainWidth, rows);
        IRenderable body = mainPanel;

        if (!narrow)
        {
            var sidebarInner = SidebarWidth - 2 - 2 * SidebarPadding;
            var sidebar = SidebarView.Build(_sidebar, Context, StepTitles, StepIndex, sidebarInner, rows - 2);

            body = new Columns(mainPanel, Frame(sidebar, "[bold white]Alexandria setup[/]", SidebarWidth, SidebarPadding)).Collapse();
        }

        _layout.Update(new Rows(new Text(string.Empty), body));
        _mainPanel = mainPanel;
    }

    private IRenderable BuildMainPanel(int columnWidth, int totalRows)
    {
        var innerWidth = Math.Max(8, columnWidth - 2 - 2 * MainPadding);
        var innerRows = Math.Max(3, totalRows - 2);
        var (content, _, _) = FixedInset(_mainContent, innerWidth, MainContentInset, MaxContentMeasure);
        var lines = SegmentLines.Render(content, innerWidth);
        var hints = new List<KeyHint>(_hints);
        var footer = Footer(hints, innerWidth);
        var available = innerRows - footer.Count;

        // Only a live window has a height to respect; dry output prints the
        // whole page so nothing is lost when stdout is not a terminal
        _mainOverflows = UseLive && lines.Count > available;

        if (_mainOverflows)
        {
            hints.Add(new KeyHint("PgUp/PgDn", "scroll"));
            footer = Footer(hints, innerWidth);
            available = innerRows - footer.Count;
            lines = Viewport(lines, Math.Max(3, available), innerWidth);
        }

        var parts = new List<IRenderable> { new SegmentLines(lines) };

        for (var i = lines.Count + footer.Count; i < innerRows; i++)
        {
            parts.Add(new Text(string.Empty));
        }

        parts.Add(new SegmentLines(footer));

        return Frame(new Rows(parts), Header(), columnWidth, MainPadding);
    }

    private List<SegmentLine> Viewport(List<SegmentLine> lines, int rows, int width)
    {
        var lastFirst = lines.Count - rows;
        _scroll = Math.Clamp(_scroll, 0, lastFirst);
        _scrollPage = rows;

        var first = _readFromTop ? _scroll : lastFirst - _scroll;
        var view = lines.Skip(first).Take(rows).ToList();

        if (first > 0)
        {
            view[0] = Marker("↑ more above", width);
        }

        if (first + rows < lines.Count)
        {
            view[^1] = Marker("↓ more below", width);
        }

        return view;
    }

    private static SegmentLine Marker(string text, int width)
    {
        var (line, _, _) = FixedInset(new Markup(Ui.Muted(text)), width, MainContentInset, MaxContentMeasure);

        return SegmentLines.Render(line, width)[0];
    }

    private List<SegmentLine> Footer(IEnumerable<KeyHint> hints, int width)
    {
        var all = hints.ToList();

        if (!GlobalsSuppressed)
        {
            all.Add(new KeyHint("T", "look"));
        }

        all.Add(new KeyHint("Ctrl+C", "quit"));

        // Wrapped by hand so a key never lands on a different line than its action
        var lines = new List<string> { string.Empty };
        var lineWidth = 0;

        foreach (var hint in all)
        {
            var cells = hint.Key.GetCellWidth() + 1 + hint.Action.GetCellWidth();
            var markup = $"[bold {Theme.Active.Ac}]{Markup.Escape(hint.Key)}[/] {Ui.Muted(hint.Action)}";

            if (lineWidth > 0 && lineWidth + 3 + cells > width)
            {
                lines.Add(string.Empty);
                lineWidth = 0;
            }

            if (lineWidth > 0)
            {
                lines[^1] += "   ";
                lineWidth += 3;
            }

            lines[^1] += markup;
            lineWidth += cells;
        }

        return SegmentLines.Render(new Rows(new Rule().RuleStyle(Theme.Active.BorderStyle), new Markup(string.Join("\n", lines))), width);
    }

    private string Header()
    {
        var title = $"[bold white]{Markup.Escape(ScreenTitle)}[/]";

        if (TotalSteps == 0)
        {
            return title;
        }

        var done = new string('█', StepIndex);
        var left = new string('░', TotalSteps - StepIndex);

        return $"[{Theme.Active.Ac}]{done}[/][{Theme.Active.Bo}]{left}[/]  {title}  {Ui.Muted($"{StepIndex}/{TotalSteps}")}";
    }

    private static Panel Frame(IRenderable content, string header, int width, int padding)
    {
        return new Panel(content)
        {
            Header = new PanelHeader($" {header} "),
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.Active.BorderStyle,
            Padding = new Padding(padding, 0, padding, 0),
            Expand = true,
            Width = width,
        };
    }

    public void Dispose()
    {
        StopFrameTimer();
    }
}
