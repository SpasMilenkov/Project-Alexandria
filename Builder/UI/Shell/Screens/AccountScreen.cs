using Builder.Models;
using Alexandria.Common.Auth;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class AccountScreen
{
    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;

        if (context.IsRepair)
        {
            shell.SetMain(new Rows(
                Views.Title("Existing admin account"),
                Views.Note($"Keeping the account for {context.AdminAccount.Email}."),
                Views.Note("Repair preserves the existing password and roles. Enter continues; Esc goes back.")));

            return IntroScreen.WaitEnter(shell) ? ScreenOutcome.Back : ScreenOutcome.Continue;
        }

        const string header0 = "This is the account you will sign in with. Write it down somewhere safe.";

        IRenderable Header(string? note = null) => new Rows(
            Views.Title("Your admin account"),
            Views.Note(header0),
            note is null ? new Markup(string.Empty) : Views.Note(note));

        var email = Widgets.AskText(shell, Header(), new TextFieldSpec
        {
            Label = "Email address",
            Initial = context.AdminAccount.Email,
            Hint = "used for signing in, e.g. you@example.com",
            Validator = AccountValidationRules.ValidateEmail,
        });

        context.AdminAccount = new AdminAccountInput
        {
            Email = email,
            Username = context.AdminAccount.Username,
            Password = context.AdminAccount.Password,
        };

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        var suggestion = AccountValidationRules.SuggestUsername(email);

        var username = Widgets.AskText(shell, Header(), new TextFieldSpec
        {
            Label = "Username",
            Initial = context.AdminUsernameCustomized ? context.AdminAccount.Username : suggestion,
            Hint = "shown inside the app; no spaces",
            Validator = AccountValidationRules.ValidateUsername,
        });

        context.AdminUsernameCustomized = context.AdminUsernameCustomized || username != suggestion;
        context.AdminAccount = new AdminAccountInput
        {
            Email = email.Trim(),
            Username = username,
            Password = context.AdminAccount.Password,
        };

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        var password = Widgets.AskText(shell, Header(), new TextFieldSpec
        {
            Label = "Password",
            Initial = context.AdminAccount.Password,
            Secret = true,
            Hint = "6+ characters, uppercase, lowercase, number, symbol",
            Validator = AccountValidationRules.ValidatePassword,
        });

        context.AdminAccount = new AdminAccountInput
        {
            Email = email.Trim(),
            Username = username,
            Password = password,
        };

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
