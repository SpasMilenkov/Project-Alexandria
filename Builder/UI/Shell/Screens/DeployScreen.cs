using System.Diagnostics;
using Builder.Services;
using Builder.Workflow;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class DeployScreen
{
    private const string GarageCredentialsFileName = "garage-credentials.env";
    private static readonly TimeSpan CredentialsWaitTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromMinutes(5);

    private sealed class Phase
    {
        public Phase(string label) => Label = label;

        public string Label { get; }
        public string State { get; set; } = "pending"; // pending|running|done|failed
        public string? Detail { get; set; }
    }

    public static ScreenOutcome Run(
        Shell shell,
        IConfigurationService configService,
        IDockerService docker)
    {
        var context = shell.Context;

        var phases = new List<Phase>
        {
            new("Writing configuration files"),
            new("Building Alexandria (first run downloads a lot)"),
            new("Starting storage and messaging"),
            new("Creating secure storage keys"),
            new("Starting Alexandria services"),
            new("Checking that everything is running"),
        };

        string? failureMessage = null;
        string? failureHint = null;

        // D53: rolling memory of the last streamed lines so the failure panel
        // can show what actually happened - even for build failures, where
        // `docker compose logs` has nothing to say
        var recentOutput = new List<string>();
        const int RecentOutputLimit = 15;

        void PushRecentOutput(string line)
        {
            recentOutput.Add(Truncate(line));

            if (recentOutput.Count > RecentOutputLimit)
            {
                recentOutput.RemoveAt(0);
            }
        }

        IRenderable Checklist()
        {
            var list = new Grid().AddColumn();

            foreach (var phase in phases)
            {
                var icon = phase.State switch
                {
                    "done" => $"[{Theme.Active.Su}]✓[/]",
                    "failed" => $"[{Theme.Active.Er}]✗[/]",
                    "running" => $"[{Theme.Active.Ac}]{Views.SpinnerFrame()}[/]",
                    _ => "[dim]○[/]",
                };

                var label = phase.State == "failed"
                    ? $"[red]{Markup.Escape(phase.Label)}[/]"
                    : Markup.Escape(phase.Label);

                list.AddRow(new Markup($"  {icon} {label}"));

                if (phase.Detail is not null)
                {
                    list.AddRow(new Markup($"      [dim]{Markup.Escape(Truncate(phase.Detail))}[/]"));
                }
            }

            return new Rows(Views.Title("Installing"), list);
        }

        bool Step(int index, Func<Action<string>, bool> action, string? failHint = null)
        {
            var phase = phases[index];

            phase.State = "running";

            var clock = Stopwatch.StartNew();
            bool ok;

            try
            {
                ok = action(line =>
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        phase.Detail = line;
                        PushRecentOutput(line);
                        Render();
                    }
                });
            }
            catch (Exception ex)
            {
                // D54: a step blowing up must land in the panel, not vanish
                ok = false;
                PushRecentOutput($"[error] {ex.Message}");
            }

            clock.Stop();

            if (ok)
            {
                phase.State = "done";
                phase.Detail = $"took {clock.Elapsed.TotalSeconds:0}s";
            }
            else
            {
                phase.State = "failed";
                failureMessage ??= $"{phase.Label} - did not finish.";

                failureHint ??= failHint
                    ?? $"Look at the logs with: cd {context.InstallPath} && docker compose logs";
            }

            Render();

            return ok;
        }

        // 1. config files (.env honoring D14, then artifacts)
        Step(0, _ => PrepareEnvironment(shell, configService));

        // 2..5 need docker output streaming into the checklist
        if (failureMessage is null)
        {
            Step(1,
                onLine => docker.ComposeBuild(context.InstallPath, l => onLine(l)),
                failHint: "First-time builds download base images - a network hiccup " +
                          "is the usual culprit. The full error shows with:\n" +
                          $"cd {context.InstallPath} && docker compose build");
        }

        if (failureMessage is null)
        {
            Step(2, onLine => docker.ComposeUpCore(context.InstallPath, l => onLine(l)));
        }

        if (failureMessage is null)
        {
            Step(3, onStatus =>
            {
                try
                {
                    return MergeGarageCredentials(shell, CredentialsPath(context), onStatus);
                }
                catch (InvalidOperationException ex)
                {
                    failureMessage = "Storage keys could not be merged.";
                    failureHint = ex.Message;

                    return false;
                }
            });
        }

        if (failureMessage is null)
        {
            Step(4, onLine => docker.ComposeUpAll(context.InstallPath, l => onLine(l)));
        }

        if (failureMessage is null)
        {
            Step(5, onStatus => WaitForStackHealthy(shell, docker, onStatus));
        }

        if (failureMessage is not null)
        {
            ShowFailure(shell, failureMessage, failureHint ?? string.Empty, context, docker, recentOutput);

            return ScreenOutcome.Abort;
        }

        return ScreenOutcome.Continue;

        void Render()
        {
            shell.SetMain(Checklist());
        }
    }

    private static string CredentialsPath(InstallationContext context)
    {
        return Path.Combine(context.InstallPath, "init-output", GarageCredentialsFileName);
    }

    private static bool PrepareEnvironment(Shell shell, IConfigurationService configService)
    {
        var context = shell.Context;

        // Belt-and-suspenders: every writer below assumes the target exists
        Directory.CreateDirectory(context.InstallPath);

        var envPath = Path.Combine(context.InstallPath, ".env");
        var existing = File.Exists(envPath) ? File.ReadAllText(envPath) : null;

        if (existing is not null)
        {
            var keep = Widgets.AskConfirm(
                shell,
                new Rows(
                    Views.Title("Existing setup found"),
                    Views.Note("This folder already has an .env file.")),
                "Keep your existing settings?",
                true);

            if (!keep)
            {
                var typed = Widgets.AskText(
                    shell,
                    Views.WarningPanel("Starting fresh replaces every setting. Your data volumes are preserved."),
                    new TextFieldSpec
                    {
                        Label = "Type FRESH to confirm",
                        Validator = v => v == "FRESH" ? null : "Type exactly FRESH to confirm, anything else cancels",
                    });

                if (typed != "FRESH")
                {
                    return true; // kept settings after all
                }

                var backup = envPath + $".bak.{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

                File.Move(envPath, backup);
                existing = null;
            }
        }

        var values = EnvGenerator.BuildValues(
            context.Features,
            context.Config.Credentials,
            context.AdminAccount,
            garageCredentials: null);

        EnvGenerator.WriteFile(envPath, EnvGenerator.MergeWithExisting(values, existing));

        configService.WriteAllConfigFiles(context.Config, context.InstallPath);

        return true;
    }

    private static bool MergeGarageCredentials(Shell shell, string credentialsPath, Action<string> onStatus)
    {
        var deadline = DateTime.UtcNow + CredentialsWaitTimeout;
        var appeared = false;
        var waited = 0;

        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(credentialsPath))
            {
                appeared = true;
                break;
            }

            // Non-blocking poll keeps [T] live during the long wait (D32)
            var key = shell.Keys.Poll();

            if (key.HasValue)
            {
                shell.HandleGlobalKey(key.Value);
            }

            Thread.Sleep(2000);
            waited += 2;
            onStatus($"waiting for storage keys... {waited}s");
        }

        if (!appeared)
        {
            return false;
        }

        var credentials = GarageCredentialsFileParser.Parse(File.ReadAllText(credentialsPath));

        if (credentials is null)
        {
            // D6: leave the file so nothing is lost
            throw new InvalidOperationException(
                "Storage key file exists but could not be read. Leave it in place and re-run the installer.");
        }

        var context = shell.Context;

        context.GarageCredentials = credentials;

        var envPath = Path.Combine(context.InstallPath, ".env");

        var merged = EnvGenerator.MergeWithExisting(
            EnvGenerator.BuildValues(context.Features, context.Config.Credentials, context.AdminAccount, credentials),
            File.ReadAllText(envPath));

        SecureDeleteAndRemove(credentialsPath);
        EnvGenerator.WriteFile(envPath, merged);

        return true;
    }

    private static void SecureDeleteAndRemove(string path)
    {
        try
        {
            var length = Math.Max(new FileInfo(path).Length, 1);

            File.WriteAllText(path, new string('\0', (int)Math.Min(length, 1 << 20)));
            File.Delete(path);
        }
        catch
        {
            // A failed wipe must not abort an otherwise healthy install
        }
    }

    private static bool WaitForStackHealthy(
        Shell shell,
        IDockerService docker,
        Action<string> onStatus)
    {
        var oneShot = new[] { "garage-init", "rabbitmq-config" };

        return docker.WaitForHealthy(
            shell.Context.InstallPath,
            HealthTimeout,
            oneShot,
            statuses =>
            {
                var up = statuses.Count(s =>
                    s.Contains("healthy", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Up ", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("Exited (0)", StringComparison.OrdinalIgnoreCase));

                onStatus($"{up}/{Math.Max(statuses.Count, up)} services ready");

                var key = shell.Keys.Poll();

                if (key.HasValue)
                {
                    shell.HandleGlobalKey(key.Value);
                }
            });
    }

    private static void ShowFailure(
        Shell shell,
        string message,
        string hint,
        InstallationContext context,
        IDockerService docker,
        IReadOnlyList<string> recentOutput)
    {
        var blocks = new List<IRenderable>
        {
            Views.ErrorPanel("Installation stopped", Markup.Escape(message)),
            Views.Note(hint),
        };

        // D53: show what actually streamed before pointing anywhere else
        if (recentOutput.Count > 0)
        {
            blocks.Add(new Rule().RuleStyle(Theme.Active.BorderStyle));

            blocks.Add(Views.ErrorPanel(
                "Last output",
                string.Join("\n", recentOutput.Select(l => Markup.Escape(l)))));
        }

        var header = new Rows(blocks.ToArray());

        // Esc on the failure menu behaves like Exit - the install already ran
        var choice = Widgets.AskSingleSelect(
            shell,
            header,
            "What now?",
            ["Show container status", "Exit"],
            s => s) ?? "Exit";

        if (choice.StartsWith("Show"))
        {
            var statuses = docker.GetContainerStatuses(context.InstallPath);

            IRenderable body = statuses.Count > 0
                ? new Rows(statuses.Select(s => (IRenderable)new Markup($"  {Markup.Escape(s)}")).ToArray())
                : new Markup("[dim]No containers were created yet - nothing to show logs from.[/]");

            shell.SetMain(new Rows(
                header,
                Views.ErrorPanel("Container status", string.Empty),
                body,
                Views.Note("Press Enter to exit.")));

            IntroScreen.WaitEnter(shell);
        }

        context.ShouldAbort = true;
    }

    private static string Truncate(string text)
    {
        return text.Length <= 70 ? text : text[..69] + "…";
    }
}
