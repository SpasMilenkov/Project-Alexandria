using System.Diagnostics;
using Builder.Models;
using Builder.Services;
using Builder.UI.Shell.Splash;
using Builder.Workflow;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class DeployScreen
{
    // The city strip only shows when the window has room for it above the
    // progress list; shorter windows keep the list alone
    internal const int CityStripRows = 7;
    private const int CityStripMinHeight = 32;

    // How fast the drawing catches up after a stage finishes, in reveal
    // seconds per real second, so each step builds in rather than jumping
    private const double CityCatchUpRate = 1.5;

    private sealed record Stage(string Label, string State, string? Detail, TimeSpan Elapsed)
    {
        public long Started { get; } = Stopwatch.GetTimestamp();
    }

    public static ScreenOutcome Run(Shell shell, IConfigurationService configuration, IDockerService docker, ISystemChecker? systemChecker = null,
        IHttpsEndpointVerifier? httpsVerifier = null, IAdminLoginVerifier? adminLoginVerifier = null)
    {
        var context = shell.Context;
        var stages = new List<Stage>();
        var recent = new Queue<string>();
        var redactor = new SecretRedactor(() => context.Config.EnvironmentValues);
        var stateGate = new object();
        var completedPreparation = new HashSet<string>(StringComparer.Ordinal);
        var finishedStages = new HashSet<string>(StringComparer.Ordinal);
        var expectedStages = InstallationOrchestrator.StageCount(ServiceCatalog.Resolve(context.Config.Features));
        var cityShown = 0.0;
        var cityClock = Stopwatch.StartNew();
        DeploymentLog log;

        try
        {
            log = new DeploymentLog(context);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            var message = redactor.Redact(error.Message);

            shell.SetMain(() => new Rows(Views.ErrorPanel("Cannot create the installation log", Markup.Escape(message)),
                    Views.Note("Deployment has not started. Check folder permissions.")),
                [new KeyHint("Enter", "back to summary")]);
            Widgets.WaitForEnter(shell);

            return ScreenOutcome.Back;
        }

        using (log)
        {
            docker.ConfigureDiagnostics(log.RecordCommand);

            try
            {
                while (true)
                {
                    try
                    {
                        shell.RunResponsive(() =>
                        {
                            new InstallationOrchestrator(configuration, docker, systemChecker, httpsVerifier,
                                adminLoginVerifier: adminLoginVerifier).Execute(context, RunStage);

                            return true;
                        }, Render);
                        log.Write("session", "Required deployment checks passed. Services keep running after exit.");

                        return ScreenOutcome.Continue;
                    }
                    catch (OperationCanceledException)
                    {
                        context.ShouldAbort = true;
                        log.Write("cancelled", "Child work stopped. Existing services and data preserved; already started services may still be running.");
                        throw;
                    }
                    catch (Exception error)
                    {
                        var safeError = redactor.Redact(error.Message);
                        log.Write("error", safeError);

                        lock (stateGate) recent.Enqueue("[error] " + safeError);

                        var failing = stages.LastOrDefault()?.Label ?? "Deployment";
                        var action = Recover(shell, docker, log, redactor, failing, safeError, recent.ToArray());

                        if (action == "Retry")
                        {
                            try
                            {
                                ValidateResume();
                            }
                            catch (Exception resumeError) when (resumeError is IOException or InvalidOperationException or FormatException)
                            {
                                var safe = redactor.Redact(resumeError.Message);
                                log.Write("recovery-error", safe);
                                Inspect(shell, "Review settings before retrying", log.FilePath, [safe]);

                                return ScreenOutcome.Back;
                            }

                            log.Write("recovery", "Retrying failed deployment; verified preparation from this session is retained.");
                            continue;
                        }

                        if (action == "Review") return ScreenOutcome.Back;

                        context.ShouldAbort = true;

                        return ScreenOutcome.Abort;
                    }
                }
            }
            finally
            {
                docker.ConfigureDiagnostics(null);
            }
        }

        void RunStage(string label, Action<Action<string>> action)
        {
            context.Cancellation.Token.ThrowIfCancellationRequested();

            if (completedPreparation.Contains(label))
            {
                lock (stateGate) finishedStages.Add(label);
                log.Write("resume", "Retaining completed preparation: " + label);
                return;
            }

            var clock = Stopwatch.StartNew();
            int index;

            lock (stateGate)
            {
                index = stages.Count;
                stages.Add(new Stage(label, "running", null, TimeSpan.Zero));
            }

            log.Write("stage", label);

            try
            {
                action(line =>
                {
                    var safeLine = redactor.Redact(line);
                    log.Write("output", safeLine);

                    lock (stateGate)
                    {
                        recent.Enqueue(safeLine);
                        while (recent.Count > 12) recent.Dequeue();
                        stages[index] = stages[index] with { Detail = safeLine, Elapsed = clock.Elapsed };
                    }
                });
                context.Cancellation.Token.ThrowIfCancellationRequested();

                lock (stateGate)
                {
                    stages[index] = stages[index] with { State = "done", Detail = null, Elapsed = clock.Elapsed };
                    finishedStages.Add(label);
                }

                if (label is "Writing configuration files" or "Preparing and verifying audio models" or "Building Alexandria")
                {
                    completedPreparation.Add(label);
                }

                log.Write("done", $"{label}; elapsed={clock.Elapsed.TotalSeconds:0.0}s");
            }
            catch
            {
                lock (stateGate) stages[index] = stages[index] with { State = "failed", Elapsed = clock.Elapsed };
                log.Write("stopped", $"{label}; elapsed={clock.Elapsed.TotalSeconds:0.0}s");
                throw;
            }
        }

        void Render()
        {
            Stage[] snapshot;
            double built;

            lock (stateGate)
            {
                snapshot = stages.TakeLast(6).ToArray();

                // A running stage counts for part of its share, so the
                // drawing moves when a long stage starts, not only when it ends
                var running = snapshot.LastOrDefault()?.State == "running" ? 0.4 : 0;

                built = Math.Min(1, (finishedStages.Count + running) / Math.Max(1, expectedStages));
            }

            var rows = new List<IRenderable>
            {
                Views.Heading("Installing Alexandria",
                    "This takes a while on the first run: images are built from your source code."),
            };
            var current = snapshot.LastOrDefault();

            // The city from the splash is laid out as the install advances
            // and only moves when a stage does; a failure leaves it where it got to
            cityShown = Math.Min(built * CityScene.Duration, cityShown + cityClock.Elapsed.TotalSeconds * CityCatchUpRate);
            cityClock.Restart();

            if (AnsiConsole.Profile.Height >= CityStripMinHeight)
            {
                rows.Add(new CityScene { Height = CityStripRows, Complete = true, Elapsed = cityShown });
                rows.Add(Views.Blank());
            }

            var grid = new Grid()
                .AddColumn(new GridColumn().NoWrap().PadRight(1))
                .AddColumn(new GridColumn().PadRight(2))
                .AddColumn(new GridColumn().NoWrap().RightAligned());

            foreach (var stage in snapshot)
            {
                var icon = stage.State switch
                {
                    "done" => Ui.Success("✓"),
                    "failed" => Ui.Error("✗"),
                    _ => $"[bold {Theme.Active.Ac}]{Views.SpinnerFrame()}[/]",
                };
                var label = stage.State == "running"
                    ? $"[bold]{Markup.Escape(stage.Label)}[/]"
                    : Markup.Escape(stage.Label);
                var elapsed = stage.State == "running" ? Stopwatch.GetElapsedTime(stage.Started) : stage.Elapsed;

                grid.AddRow(new Markup(icon), new Markup(label), new Markup(Ui.Muted($"{elapsed.TotalSeconds:0}s")));
            }

            rows.Add(grid);

            if (current?.Detail is not null)
            {
                var detail = current.Detail.Length > 600 ? current.Detail[..600] : current.Detail;

                rows.Add(Views.Blank());
                rows.Add(new Callout(new Text(detail, Theme.Active.MutedStyle), Theme.Active.BorderStyle));
            }

            rows.Add(Views.Blank());
            rows.Add(Views.Note("Ctrl+C stops child work and preserves services and data."));
            rows.Add(Views.Note("Log: " + log.FilePath));
            shell.SetMain(new Rows(rows), []);
        }

        void ValidateResume()
        {
            var path = Path.Combine(context.InstallPath, ".env");

            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                var expected = EnvGenerator.Parse(content);

                if (expected.Count != context.Config.EnvironmentValues.Count ||
                    context.Config.EnvironmentValues.Any(pair => !expected.TryGetValue(pair.Key, out var value) || value != pair.Value))
                {
                    throw new InvalidOperationException("Settings changed during deployment. Return to the summary and review them before retrying.");
                }

                context.ApprovedEnvironmentContent = content;
            }
            else if (completedPreparation.Contains("Writing configuration files"))
            {
                throw new InvalidOperationException("The environment file disappeared. Return to the summary before retrying.");
            }

            docker.VerifyInstallationOwnership(context.InstallPath, context.Config);

            if (File.Exists(Path.Combine(context.InstallPath, InstallationConfigurationResolver.GenerationMarkerName)))
            {
                var inspection = InstallationConfigurationResolver.Inspect(context);

                if (inspection.IsIncompleteGeneration || inspection.SavedConfiguration?.GenerationId != context.Config.GenerationId)
                {
                    throw new InvalidOperationException("Generated files changed or are incomplete. Return to the summary to review and regenerate them before retrying.");
                }
            }
        }
    }

    private static string Recover(Shell shell, IDockerService docker, DeploymentLog log, SecretRedactor redactor,
        string stage, string error, IReadOnlyList<string> recent)
    {
        var retryLabel = stage switch
        {
            "Building Alexandria" => "Retry image build",
            "Preparing and verifying audio models" => "Retry model preparation",
            "Writing configuration files" or "Validating the deployment" => "Retry configuration",
            _ => "Resume bootstrap and readiness checks",
        };

        while (true)
        {
            var last = recent.LastOrDefault(line => !line.StartsWith("[error]", StringComparison.Ordinal)) ?? error;
            var choice = ChooseRecovery(shell, error, last, stage, log.FilePath, retryLabel);

            if (choice == retryLabel) return "Retry";
            if (choice == "Return to summary") return "Review";
            if (choice is null or "Exit preserving services") return "Exit";

            try
            {
                IReadOnlyList<string> lines;

                if (choice == "Show container status")
                {
                    lines = shell.RunResponsive(() => docker.GetContainerStatuses(shell.Context.InstallPath));
                    if (lines.Count == 0) lines = ["No containers were created yet."];
                }
                else if (choice == "Inspect service logs")
                {
                    var service = Widgets.AskSingleSelect(shell, Views.Title("Service logs"), "Choose a service",
                        ServiceCatalog.Resolve(shell.Context.Features).ServiceIds, value => value);

                    if (service is null) continue;

                    lines = shell.RunResponsive(() => docker.GetServiceLogs(shell.Context.InstallPath, service, 200));
                }
                else
                {
                    lines = recent.Concat(log.ReadRecent()).ToArray();
                }

                var safe = lines.Select(redactor.Redact).ToArray();

                if (choice != "Inspect installation log")
                {
                    foreach (var line in safe) log.Write("inspection", line);
                }

                Inspect(shell, choice, log.FilePath, safe);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception inspectionError)
            {
                var safe = redactor.Redact(inspectionError.Message);
                log.Write("inspection-error", safe);
                Inspect(shell, "Inspection unavailable", log.FilePath, [safe]);
            }
        }
    }

    private static string? ChooseRecovery(Shell shell, string error, string last, string stage, string logPath, string retryLabel)
    {
        string[] choices = ["Show container status", retryLabel, "Inspect installation log", "Inspect service logs", "Return to summary", "Exit preserving services"];
        var index = 0;

        while (true)
        {
            var width = Shell.MainContentWidth;
            var roomy = AnsiConsole.Profile.Height >= 26;
            var menu = new Grid().AddColumn();

            for (var i = 0; i < choices.Length; i++)
            {
                menu.AddRow(new Markup(i == index
                    ? $"[bold {Theme.Active.Ac}]❯ {Markup.Escape(choices[i])}[/]"
                    : "  " + Markup.Escape(choices[i])));
            }

            var facts = new List<(string Label, string Value)> { ("Stage", stage) };

            if (roomy)
            {
                facts.Add(("Last output", last));
                facts.Add(("Next attempt", stage == "Building Alexandria" ? "Runs docker compose build again." : "Uses the saved project and settings."));
            }

            facts.Add(("Log", logPath));

            // Short terminals drop the spacing and the closing note so the
            // error and the whole menu still fit on one screen
            var rows = new List<IRenderable> { Views.Title("Installation stopped") };

            if (roomy)
            {
                rows.Add(Views.Blank());
            }

            rows.Add(Views.ErrorPanel("Failed", Markup.Escape(error[..Math.Min(error.Length, width * 2)])));

            if (!roomy)
            {
                rows.Add(new Text(last[..Math.Min(last.Length, width * 2)], Theme.Active.MutedStyle));
            }

            rows.Add(Views.Facts(facts));
            rows.Add(Views.Blank());
            rows.Add(Views.Title("What now?"));
            rows.Add(menu);

            if (roomy)
            {
                rows.Add(Views.Blank());
                rows.Add(Views.Note("Services and data are preserved; started services keep running."));
            }

            shell.SetMain(new Rows(rows),
                [new KeyHint("↑↓", "choose"), new KeyHint("Enter", "select"), new KeyHint("L", "full log"), new KeyHint("Esc", "exit")]);
            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key)) continue;
            if (key.Key == ConsoleKey.Escape) return null;
            if (key.Key == ConsoleKey.Enter) return choices[index];
            if (key.Key == ConsoleKey.L) return "Inspect installation log";
            if (key.Key == ConsoleKey.UpArrow) index = (index - 1 + choices.Length) % choices.Length;
            if (key.Key == ConsoleKey.DownArrow) index = (index + 1) % choices.Length;
        }
    }

    internal static void Inspect(Shell shell, string title, string logPath, IReadOnlyList<string> lines)
    {
        var offset = 0;

        while (true)
        {
            var width = Shell.MainContentWidth;
            var wrapped = lines.SelectMany(line => Wrap(line, width)).ToArray();

            // Frame borders, heading, log path and the key bar take the rest
            var pageSize = Math.Max(1, AnsiConsole.Profile.Height - 10);
            offset = Math.Clamp(offset, 0, Math.Max(0, wrapped.Length - pageSize));
            shell.SetMain(new Rows(
                    new Markup($"[bold]{Markup.Escape(title)}[/]  {Ui.Muted($"lines {offset + 1}–{Math.Min(wrapped.Length, offset + pageSize)} of {wrapped.Length}")}"),
                    new Text(string.Join("\n", wrapped.Skip(offset).Take(pageSize))),
                    Views.Note("Log: " + logPath)),
                [new KeyHint("↑↓", "scroll"), new KeyHint("PgUp/PgDn", "page"), new KeyHint("Home/End", "jump"), new KeyHint("Enter", "back")]);
            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key)) continue;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Escape) return;

            offset = key.Key switch
            {
                ConsoleKey.DownArrow => offset + 1,
                ConsoleKey.UpArrow => offset - 1,
                ConsoleKey.PageDown => offset + pageSize,
                ConsoleKey.PageUp => offset - pageSize,
                ConsoleKey.End => wrapped.Length - pageSize,
                ConsoleKey.Home => 0,
                _ => offset,
            };
        }
    }

    private static IEnumerable<string> Wrap(string line, int width)
    {
        var remaining = line.AsMemory();

        while (remaining.Length > width)
        {
            var boundary = remaining.Span[..width].LastIndexOf(' ');
            if (boundary < width / 2) boundary = width;

            yield return remaining[..boundary].ToString();
            remaining = remaining[boundary..];
            if (remaining.Span[0] == ' ') remaining = remaining[1..];
        }

        yield return remaining.ToString();
    }
}
