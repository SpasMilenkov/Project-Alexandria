using System.Text.Json;
using Builder.Models;

namespace Builder.Services;

// Loads and saves the installer's appearance preference from the user-level
// config directory. Reading failures fall back to defaults silently (a broken
// preference must never block install); saving failures are reported to the
// caller so the user can be told their choice will not stick.
public static class UiPreferencesStore
{
    private const string FileName = "theme.json";

    public static string? Load()
    {
        return LoadFrom(PreferencesDirectory());
    }

    public static bool Save(string themeId)
    {
        return SaveTo(PreferencesDirectory(), themeId);
    }

    internal static string? LoadFrom(string directory)
    {
        try
        {
            var path = Path.Combine(directory, FileName);

            if (!File.Exists(path))
            {
                return null;
            }

            var preferences = JsonSerializer.Deserialize(
                File.ReadAllText(path),
                AlexandriaJsonContext.Default.UiPreferences);

            return string.IsNullOrWhiteSpace(preferences?.Theme) ? null : preferences.Theme;
        }
        catch
        {
            // Corrupt or unreadable preferences fall back to the default look
            return null;
        }
    }

    internal static bool SaveTo(string directory, string themeId)
    {
        try
        {
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(
                new UiPreferences { Theme = themeId },
                AlexandriaJsonContext.Default.UiPreferences);

            File.WriteAllText(Path.Combine(directory, FileName), json);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string PreferencesDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return Path.Combine(baseDir, "alexandria-installer");
    }
}
