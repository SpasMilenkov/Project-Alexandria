using Builder.UI;
using Builder.UI.Shell;
using Builder.Workflow;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Builder.Tests.Support;

// Boots a Shell against an interactive TestConsole with a scripted keyboard,
// restoring the global console afterwards. Sequential execution is enforced
// assembly-wide (see TestAssembly.cs) because of the global swap.
public sealed class ShellHarness : IDisposable
{
    private readonly IAnsiConsole _original;

    public TestConsole Console { get; }
    public IKeySource Keys { get; }
    public InstallationContext Context { get; }
    public Shell Shell { get; }

    public ShellHarness(IKeySource keys)
    {
        Keys = keys;
        _original = AnsiConsole.Console;

        Console = new TestConsole();
        Console.Interactive();
        AnsiConsole.Console = Console;

        Context = new InstallationContext();
        Context.Config.PublicEndpoint = new Builder.Models.PublicEndpoint { Host = "192.0.2.10", HttpsPort = 443 };
        Context.Config.ProjectName = "builder-ui-test";

        Shell = new Shell(Context, keys)
        {
            // Live buffers frames away from TestConsole.Output; dry mode lets
            // tests assert rendered content directly
            EnableLive = false,
        };
    }

    public string Output => Console.Output;

    public void Dispose()
    {
        Shell.Dispose();
        AnsiConsole.Console = _original;
        Theme.Use(Themes.Default);
    }
}
