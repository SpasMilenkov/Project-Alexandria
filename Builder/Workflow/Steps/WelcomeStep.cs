using Builder.UI;
using Environment = Builder.Models.Environment;

namespace Builder.Workflow.Steps;

public class WelcomeStep : WizardStep
{
    public override string Title => "Welcome";
    public override int StepNumber => 1;

    protected override void Run(InstallationContext context)
    {
        AnsiConsole.Clear();  // ← add this
        Components.Banner();
        Components.Spacer();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"  [{Theme.Di}]Select your environment[/]")
                .HighlightStyle(Theme.AccentStyle)
                .AddChoices(
                    "Production deploy",
                    "Local preview",
                    "Development"));

        var env = choice switch
        {
            "Production deploy" => Environment.Deployment,
            "Local preview" => Environment.LocalPreview,
            "Development" => Environment.Development,
            _ => Environment.Deployment
        };

        context.Config.Environment = env;

        if (env == Environment.Development)
        {
            Components.Spacer();
            Components.InfoPanel("Development Mode",
                $"[{Theme.Di}]Development mode uses docker-compose.dev.yml directly.\n" +
                $"Run [bold white]docker compose -f docker-compose.dev.yml up -d[/] from the project root.[/]");
            context.ShouldAbort = true;
        }
    }
}
