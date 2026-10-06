global using Spectre.Console;
using Builder.Services;
using Builder.UI;
using Builder.Workflow;

Theme.Use(Themes.Resolve(UiPreferencesStore.Load()));

var flow = new MainFlow();

try
{
    if (!InstallerArguments.Apply(args, flow.Context))
    {
        Console.WriteLine(InstallerArguments.Usage);
        flow.Context.Cancellation.Dispose();
        return 0;
    }
}
catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException or FormatException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine(ex.Message);
    flow.Context.Cancellation.Dispose();
    return 2;
}

ConsoleCancelEventHandler cancelHandler = (_, e) =>
{
    e.Cancel = true;
    flow.Cancel();
};

Console.CancelKeyPress += cancelHandler;

try
{
    flow.ExecuteFlow();

    if (flow.Context.Cancellation.IsCancellationRequested)
    {
        AnsiConsole.MarkupLine("[yellow]Installation cancelled. Existing services and data were preserved.[/]");

        return 130;
    }

    return flow.Context.Completed && !flow.Context.ShouldAbort ? 0 : 1;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
    flow.Context.Cancellation.Dispose();
}
