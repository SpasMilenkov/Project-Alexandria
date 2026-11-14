using Alexandria.Common.Auth;
using Builder.Credentials;
using Builder.UI.Shell.Input;
using Builder.UI.Shell.Screens.Components;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class AccountScreen
{
    // Fixed length, so the summary of entered fields never reveals the
    // password's length
    private const string MaskedPassword = "••••••••";

    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;

        if (context.IsRepair)
        {
            shell.SetMain(() => new Rows(
                Views.Heading("Existing admin account",
                    "Repair preserves the existing password and roles."),
                Views.Facts([("Email", context.AdminAccount.Email)])));

            return Widgets.WaitForEnter(shell) ? ScreenOutcome.Back : ScreenOutcome.Continue;
        }

        IRenderable Header(string? note, params (string Label, string Value)[] entered)
        {
            var rows = new List<IRenderable>
            {
                Views.Heading("Your admin account",
                    "This is the account you will sign in with. Write it down somewhere safe."),
            };

            if (entered.Length > 0)
            {
                rows.Add(Views.CheckList(entered.Select(field => (field.Label, true, (string?)field.Value))));
                rows.Add(Views.Blank());
            }

            if (note is not null)
            {
                rows.Add(Views.Note(note));
            }

            return new Rows(rows);
        }

        var email = Widgets.AskText(shell, Header(null), new TextFieldSpec
        {
            Label = "Email address",
            Initial = context.AdminAccount.Email,
            Hint = "Used for signing in, e.g. you@example.com",
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

        var username = Widgets.AskText(shell, Header(null, ("Email", email.Trim())), new TextFieldSpec
        {
            Label = "Username",
            Initial = context.AdminUsernameCustomized ? context.AdminAccount.Username : suggestion,
            Hint = "Shown inside the app; no spaces",
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

        var password = Widgets.AskText(shell, Header(null, ("Email", email.Trim()), ("Username", username)), new TextFieldSpec
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

        _ = Widgets.AskText(shell,
            Header("Repeat the password to make sure it matches.",
                ("Email", email.Trim()), ("Username", username), ("Password", MaskedPassword)),
            new TextFieldSpec
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
