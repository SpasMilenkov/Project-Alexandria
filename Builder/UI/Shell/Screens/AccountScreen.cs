using Builder.Models;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class AccountScreen
{
    public static ScreenOutcome Run(Shell shell)
    {
        const string header0 = "This is the account you will sign in with. Write it down somewhere safe.";

        IRenderable Header(string? note = null) => new Rows(
            Views.Title("Your admin account"),
            Views.Note(header0),
            note is null ? new Markup(string.Empty) : Views.Note(note));

        var email = Widgets.AskText(shell, Header(), new TextFieldSpec
        {
            Label = "Email address",
            Hint = "used for signing in, e.g. you@example.com",
            Validator = v => v.Contains('@') && v.Contains('.')
                ? null
                : "Please enter a valid email address",
        });

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        var username = Widgets.AskText(shell, Header(), new TextFieldSpec
        {
            Label = "Display name",
            Initial = email.Split('@')[0],
            Hint = "shown inside the app",
            Validator = v => v.Trim().Length >= 2
                ? null
                : "Please use at least 2 characters",
        });

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        var password = Widgets.AskText(shell, Header(), new TextFieldSpec
        {
            Label = "Password",
            Secret = true,
            Hint = "at least 8 characters",
            Validator = v => v.Length >= 8 ? null : "Please use at least 8 characters",
        });

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        _ = Widgets.AskText(shell, Header("Repeat the password to make sure it matches"), new TextFieldSpec
        {
            Label = "Repeat password",
            Secret = true,
            Validator = v => v == password ? null : "Passwords do not match - start again",
        });

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        shell.Context.AdminAccount = new AdminAccountInput
        {
            Email = email.Trim(),
            Username = username.Trim(),
            Password = password,
        };

        return ScreenOutcome.Continue;
    }
}
