using Builder.UI;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class SuccessScreen
{
    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;

        var parts = new List<IRenderable>
        {
            Views.Banner(),
            Views.InfoPanel("Alexandria is up and running", string.Empty),
            Views.SuccessGrid(context),
        };

        if (context.Config.OriginalPorts.Count > 0)
        {
            var moved = new Grid().AddColumn().AddColumn();

            foreach (var mapping in context.PortMappings.Where(m => m.AssignedPort != m.DefaultPort))
            {
                moved.AddRow(
                    new Markup($"[bold]{mapping.ServiceName}[/]"),
                    new Markup($"{mapping.DefaultPort} → [{Theme.Active.Ac}]{mapping.AssignedPort}[/]"));
            }

            parts.Add(Views.Title("Moved ports"));
            parts.Add(moved);
        }

        parts.Add(Views.Note("Sign in with the email and password you chose earlier."));
        parts.Add(Views.Note("Press Enter to close the installer - Alexandria keeps running."));
        shell.SetMain(new Rows(parts.ToArray()));

        IntroScreen.WaitEnter(shell);

        return ScreenOutcome.Continue;
    }
}
