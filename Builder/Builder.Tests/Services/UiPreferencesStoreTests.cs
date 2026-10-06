using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Services;

public class UiPreferencesStoreTests : IDisposable
{
    private readonly string _directory;

    public UiPreferencesStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "builder-prefs-tests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void SaveThenLoad_roundTripsThemeId()
    {
        UiPreferencesStore.SaveTo(_directory, "aurora");

        var loaded = UiPreferencesStore.LoadFrom(_directory);

        loaded.Should().Be("aurora");
    }

    [Fact]
    public void Load_missingFile_returnsNull()
    {
        var loaded = UiPreferencesStore.LoadFrom(_directory);

        loaded.Should().BeNull();
    }

    [Fact]
    public void Load_corruptFile_returnsNullInsteadOfThrowing()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "theme.json"), "{ not json at all ");

        var loaded = UiPreferencesStore.LoadFrom(_directory);

        loaded.Should().BeNull();
    }

    [Fact]
    public void Load_unknownThemeName_returnsTheRawValueForCallerResolution()
    {
        Directory.CreateDirectory(_directory);

        File.WriteAllText(
            Path.Combine(_directory, "theme.json"),
            """{"theme":"retro-green"}""");

        // Store returns what was persisted; Themes.Resolve maps unknown ids
        // to the default. The split keeps the store free of UI knowledge.
        var loaded = UiPreferencesStore.LoadFrom(_directory);

        loaded.Should().Be("retro-green");
    }

    [Fact]
    public void Save_toUnwritableDirectory_returnsFalse()
    {
        // A file where the directory should be forces the create/write to fail
        var blocker = Path.Combine(_directory, "blocker");

        Directory.CreateDirectory(_directory);
        File.WriteAllText(blocker, "not a directory");

        var targetDirectory = Path.Combine(blocker, "nested");

        var saved = UiPreferencesStore.SaveTo(targetDirectory, "aurora");

        saved.Should().BeFalse();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // temp cleanup is best effort
        }
    }
}
