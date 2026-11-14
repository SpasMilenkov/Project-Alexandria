using Builder.Certificates;
using Builder.UI.Shell.Input;
using Builder.UI.Shell.Screens.Components;
using Builder.Workflow;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class CertificateScreen
{
    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;
        var settings = context.Config.Certificates;
        var options = new[] { "Generate and reuse an installation certificate", "Import an existing certificate and key" };
        var selected = settings.Mode == CertificateSettings.ImportedMode ? options.Reverse().ToArray() : options;
        const string keepSaved = "Keep the saved certificate settings";

        if (CanKeepSavedImport()) selected = [keepSaved, .. selected];

        IRenderable Header() => Views.Heading("HTTPS certificate",
            "HTTPS protects sign-in and encrypted files. Your devices must trust the certificate before you use Alexandria.");

        while (true)
        {
            var choice = Widgets.AskSingleSelect(shell, Header(), "Certificate", selected, value => value,
                detailFor: value => Views.Note(value switch
                {
                    keepSaved => "Reuse the certificate and key this installation already imported.",
                    _ when value == options[1] => "Use a certificate your devices already trust. Its files are copied into the setup folder.",
                    _ => "Setup creates a local CA in your setup folder. Each device trusts it once, following nginx/CLIENT-TRUST.txt.",
                }));

            if (choice is null) return ScreenOutcome.Back;

            if (choice != keepSaved)
            {
                settings.Mode = choice == options[1] ? CertificateSettings.ImportedMode : CertificateSettings.GeneratedMode;
            }

            if (settings.Mode == CertificateSettings.ImportedMode && choice != keepSaved)
            {
                if (!AskPath("Certificate PEM file", settings.ImportCertificatePath, value => settings.ImportCertificatePath = value) ||
                    !AskPath("Private key PEM file", settings.ImportPrivateKeyPath, value => settings.ImportPrivateKeyPath = value))
                {
                    return ScreenOutcome.Back;
                }
            }

            try
            {
                new CertificateService().Validate(context.InstallPath, context.Config.PublicEndpoint.Host, settings);

                return ScreenOutcome.Continue;
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
            {
                var message = exception.Message;

                shell.SetMain(() => new Rows(Header(), Views.ErrorPanel("Certificate needs attention", Markup.Escape(message)),
                        Views.Note("Existing certificate files are preserved.")),
                    [new KeyHint("Enter", "change choices"), new KeyHint("Esc", "back")]);

                if (Widgets.WaitForEnter(shell)) return ScreenOutcome.Back;
            }
        }

        bool CanKeepSavedImport()
        {
            if (settings.Mode != CertificateSettings.ImportedMode ||
                !File.Exists(Path.Combine(context.InstallPath, "nginx/certs/server.crt"))) return false;

            try
            {
                new CertificateService().Validate(context.InstallPath, context.Config.PublicEndpoint.Host, settings);

                return true;
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
            {
                return false;
            }
        }

        bool AskPath(string label, string initial, Action<string> store)
        {
            var path = Widgets.AskText(shell, Header(), new TextFieldSpec
            {
                Label = label,
                Initial = initial,
                Hint = "An existing PEM file. Its contents are copied into this installation's protected certificate folder.",
                Validator = value =>
                {
                    if (!PathResolver.TryResolve(value, context.LaunchDirectory, out var resolved, out var error)) return error;

                    return File.Exists(resolved) ? null : "This file does not exist.";
                },
            });

            if (shell.TakeEscape()) return false;

            PathResolver.TryResolve(path, context.LaunchDirectory, out var fullPath, out _);
            store(fullPath);

            return true;
        }
    }
}
