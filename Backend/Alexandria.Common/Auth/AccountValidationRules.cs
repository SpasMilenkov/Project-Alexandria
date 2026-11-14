using System.ComponentModel.DataAnnotations;

namespace Alexandria.Common.Auth;

public static class AccountValidationRules
{
    public const int PasswordLength = 6;
    public const int PasswordUniqueCharacters = 1;
    public const string UsernameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public static string? ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Any(char.IsControl) ||
            !new EmailAddressAttribute().IsValid(email.Trim()))
        {
            return "Please enter a valid email address";
        }

        return null;
    }

    public static string? ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Any(c => !UsernameCharacters.Contains(c)))
        {
            return "Use letters A-Z, numbers, or - . _ @ + without spaces";
        }

        return null;
    }

    public static string? ValidatePassword(string password)
    {
        if (password.Length < PasswordLength || string.IsNullOrWhiteSpace(password))
        {
            return $"Please use at least {PasswordLength} characters";
        }

        if (password.Any(char.IsControl))
        {
            return "Passwords cannot contain control characters or line breaks";
        }

        if (!password.Any(char.IsAsciiLetterUpper) || !password.Any(char.IsAsciiLetterLower) ||
            !password.Any(char.IsAsciiDigit) || password.All(char.IsAsciiLetterOrDigit))
        {
            return "Include an uppercase letter, a lowercase letter, a number, and a symbol";
        }

        return null;
    }

    public static string SuggestUsername(string email)
    {
        var localPart = email.Split('@')[0];
        var suggestion = new string(localPart.Where(UsernameCharacters.Contains).ToArray());

        return string.IsNullOrWhiteSpace(suggestion) ? "admin" : suggestion;
    }
}
