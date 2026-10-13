using Builder.Models;
using Builder.Services;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class PortsScreen
{
    public static ScreenOutcome Run(Shell shell, IPortResolver resolver, ISystemChecker checker)
    {
        var context = shell.Context;
        var endpoint = context.Config.PublicEndpoint;
        var suggestedHost = string.IsNullOrEmpty(endpoint.Host) ? PublicEndpoint.SuggestHost() : endpoint.Host;
        var host = Widgets.AskText(shell, new Rows(
            Views.Title("Access from your network"),
            Views.Note("Alexandria uses one secure HTTPS port for the website, API, files, and optional monitoring."),
            Views.Note("Use this computer's LAN IPv4 address or a DNS name that your devices can resolve. Keep the address stable.")),
            new TextFieldSpec
            {
                Label = "LAN address or DNS name",
                Initial = context.PublicHostDraft ?? suggestedHost,
                Hint = "For example, 192.168.0.106. Enter a host only, without https:// or a port.",
                Validator = value => PublicEndpoint.TryNormalizeHost(value, out _, out var error) ? null : error,
            });
        context.PublicHostDraft = host;

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        PublicEndpoint.TryNormalizeHost(host, out host, out _);
        var mappings = resolver.ResolveAll(context.Features, checker);
        var mapping = mappings.Single();
        var savedPort = context.Config.Ports.GetValueOrDefault("HTTPS_PORT", endpoint.HttpsPort);

        if (savedPort is < 1 or > 65535) throw new InvalidOperationException("The saved HTTPS port is invalid. Inspect the installation metadata before repair.");

        mapping.AssignedPort = savedPort;
        mapping.IsConflicted = !IsAvailable(savedPort);
        context.PortMappings = mappings;

        IRenderable Header(string message) => new Rows(
            Views.Title("One secure port"),
            Views.Note("A port is a numbered network connection. Alexandria publishes one; its internal services stay private."),
            Views.Note(message),
            new Markup(Markup.Escape(new PublicEndpoint { Host = host, HttpsPort = mapping.AssignedPort }.Origin)));

        var options = mapping.IsConflicted
            ? new[] { "Pick a free HTTPS port", "Choose another port" }
            : new[] { "Keep this HTTPS port", "Choose another port" };
        var choice = Widgets.AskSingleSelect(shell,
            Header(mapping.IsConflicted ? $"Port {savedPort} is busy. The first fallback is 8443." : "Keep this address or choose a different port."),
            "HTTPS port", options, value => value);

        if (choice is null) return ScreenOutcome.Back;

        if (choice == "Pick a free HTTPS port")
        {
            resolver.AutoRemap(mappings);

            if (mapping.IsConflicted) throw new InvalidOperationException("No available HTTPS port was found. Free a port and retry setup.");
        }
        else if (choice == "Choose another port")
        {
            var raw = Widgets.AskText(shell, Header("Choose the port your devices will use."), new TextFieldSpec
            {
                Label = "HTTPS port",
                Initial = mapping.AssignedPort.ToString(),
                Hint = "1–65535; 8443 is a convenient alternative to 443.",
                Validator = value =>
                {
                    if (!int.TryParse(value, out var port) || port is < 1 or > 65535) return "Enter a number between 1 and 65535.";

                    return IsAvailable(port) ? null : $"Port {port} is already in use.";
                },
            });

            if (shell.TakeEscape()) return ScreenOutcome.Back;

            mapping.AssignedPort = int.Parse(raw);
            mapping.IsConflicted = false;
        }

        context.Config.PublicEndpoint = new PublicEndpoint { Host = host, HttpsPort = mapping.AssignedPort };
        context.Config.Ports = new Dictionary<string, int> { ["HTTPS_PORT"] = mapping.AssignedPort };
        context.Config.OriginalPorts = mapping.AssignedPort == 443
            ? new Dictionary<string, int>()
            : new Dictionary<string, int> { ["HTTPS_PORT"] = 443 };
        shell.SetMain(new Rows(Header("Your devices will use this HTTPS address."),
            Views.Note("Any previous HTTP or separate monitoring port will close when the new configuration starts."),
            Views.Note("Press Enter to continue; Esc returns to your choices.")));

        return IntroScreen.WaitEnter(shell) ? ScreenOutcome.Back : ScreenOutcome.Continue;

        bool IsAvailable(int port) => context.IsRepair && context.OwnedHttpsPort == port || checker.IsPortAvailable(port);
    }
}
