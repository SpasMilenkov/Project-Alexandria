global using Spectre.Console;
using Builder.Workflow;

var flow = new MainFlow();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    flow.Cleanup();
    AnsiConsole.MarkupLine("\n[yellow]Installation cancelled by user.[/]");
    System.Environment.Exit(1);
};

flow.ExecuteFlow();
