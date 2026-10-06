using System.Diagnostics;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class DeployScreen
{
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
        DeploymentLog log;

        try
        {
            log = new DeploymentLog(context);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            shell.SetMain(new Rows(Views.ErrorPanel("Cannot create the installation log", Markup.Escape(redactor.Redact(error.Message))),
                Views.Note("Deployment has not started. Check folder permissions. Press Enter to return to the summary.")));
            IntroScreen.WaitEnter(shell);

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

                lock (stateGate) stages[index] = stages[index] with { State = "done", Detail = null, Elapsed = clock.Elapsed };

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

            lock (stateGate) snapshot = stages.TakeLast(4).ToArray();

            var rows = new List<IRenderable> { Views.Title("Installing Alexandria") };
            var current = snapshot.LastOrDefault();

            if (current is not null)
            {
                rows.Add(Views.Note(current.Label));

                if (current.Detail is not null) rows.Add(new Text(current.Detail.Length > 600 ? current.Detail[..600] : current.Detail));
            }

            foreach (var stage in snapshot)
            {
                var icon = stage.State switch
                {
                    "done" => $"[{Theme.Active.Su}]✓[/]",
                    "failed" => $"[{Theme.Active.Er}]✗[/]",
                    _ => $"[{Theme.Active.Ac}]{Views.SpinnerFrame()}[/]",
                };
                var elapsed = stage.State == "running" ? Stopwatch.GetElapsedTime(stage.Started) : stage.Elapsed;
                rows.Add(new Markup($"{icon} {Markup.Escape(stage.Label)} [dim]{elapsed.TotalSeconds:0}s[/]"));
            }

            rows.Add(Views.Note("T changes the look · Ctrl+C stops child work and preserves services/data."));
            rows.Add(Views.Note("Log: " + log.FilePath));
            shell.SetMain(new Rows(rows));
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
            var width = ContentWidth();
            var rows = new List<IRenderable>
            {
                Views.Title("Installation stopped"),
                new Text(error[..Math.Min(error.Length, width * 2)]),
                new Text(last[..Math.Min(last.Length, width * 2)]),
                Views.Note("What now? ↑/↓ choose · Enter selects · L opens full evidence · Esc exits"),
                new Markup($"[{Theme.Active.Ac}]▸ {Markup.Escape(choices[index])}[/]"),
                Views.Note("Services/data preserved; started services keep running."),
                new Text("Log: " + logPath),
            };

            if (AnsiConsole.Profile.Height >= 26)
            {
                rows.Add(Views.Note("Stage: " + stage));
                rows.Add(Views.Note("Last output: " + last));
                rows.Add(Views.Note(stage == "Building Alexandria" ? "The failing command was docker compose build." : "Retry uses the saved project and settings."));
                rows.Add(Views.Note(string.Join("\n", choices.Where((_, item) => item != index))));
            }

            shell.SetMain(new Rows(rows));
            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key)) continue;
            if (key.Key == ConsoleKey.Escape) return null;
            if (key.Key == ConsoleKey.Enter) return choices[index];
            if (key.Key == ConsoleKey.L) return "Inspect installation log";
            if (key.Key == ConsoleKey.UpArrow) index = (index - 1 + choices.Length) % choices.Length;
            if (key.Key == ConsoleKey.DownArrow) index = (index + 1) % choices.Length;
        }
    }

    private static int ContentWidth()
    {
        var available = AnsiConsole.Profile.Width - 18;
        if (AnsiConsole.Profile.Width >= Shell.MinFullWidth) available -= Shell.SidebarWidth;

        return Math.Max(12, Math.Min(Shell.MaxContentMeasure, available));
    }

    internal static void Inspect(Shell shell, string title, string logPath, IReadOnlyList<string> lines)
    {
        var offset = 0;

        while (true)
        {
            var width = ContentWidth();
            var wrapped = lines.SelectMany(line => Wrap(line, width)).ToArray();
            var pageSize = Math.Max(1, AnsiConsole.Profile.Height - 10);
            offset = Math.Clamp(offset, 0, Math.Max(0, wrapped.Length - pageSize));
            shell.SetMain(new Rows(Views.Title(title), new Text($"Lines {offset + 1}–{Math.Min(wrapped.Length, offset + pageSize)} / {wrapped.Length}"),
                new Text(string.Join("\n", wrapped.Skip(offset).Take(pageSize))),
                Views.Note("↑/↓ scroll · PgUp/PgDn page · Enter/Esc returns"), Views.Note("Log: " + logPath)));
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
