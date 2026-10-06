global using Spectre.Console;
using Builder.Services;
using Builder.UI;
using Builder.Workflow;

// Resolve the persisted look before anything renders (locked decision D28)
Theme.Use(Themes.Resolve(UiPreferencesStore.Load()));

var flow = new MainFlow();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    flow.Cleanup();
    AnsiConsole.MarkupLine("\n[yellow]Installation cancelled by user.[/]");
    System.Environment.Exit(1);
};

flow.ExecuteFlow();
