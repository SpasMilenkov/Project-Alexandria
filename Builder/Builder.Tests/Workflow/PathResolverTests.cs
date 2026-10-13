using AwesomeAssertions;
using Builder.Models;
using Builder.Workflow;

namespace Builder.Tests.Workflow;

public class PathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-path-tests", Guid.NewGuid().ToString("N"));

    public PathResolverTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData(".", ".")]
    [InlineData("settings", "settings")]
    [InlineData(" ./settings with spaces/ ", "settings with spaces")]
    [InlineData("\"settings with spaces\"", "settings with spaces")]
    [InlineData("  'settings with spaces'  ", "settings with spaces")]
    [InlineData("one/../settings/", "settings")]
    [InlineData("設定/музыка", "設定/музыка")]
    public void TryResolve_relativeInput_usesSuppliedLaunchDirectory(string input, string expectedRelative)
    {
        var success = PathResolver.TryResolve(input, _root, out var resolved, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        resolved.Should().Be(Path.GetFullPath(Path.Combine(_root, expectedRelative)));
    }

    [Fact]
    public void TryResolve_absolutePath_retainsAbsoluteLocation()
    {
        var expected = Path.Combine(_root, "absolute folder");

        PathResolver.TryResolve(expected + Path.DirectorySeparatorChar, Path.GetTempPath(), out var resolved, out var error)
            .Should().BeTrue();

        resolved.Should().Be(expected);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("~", "")]
    [InlineData("~/settings with spaces", "settings with spaces")]
    [InlineData("~//settings", "settings")]
    [InlineData("'~/settings'", "settings")]
    public void TryResolve_homeNotation_usesCurrentUserHome(string input, string suffix)
    {
        PathResolver.TryResolve(input, _root, out var resolved, out var error).Should().BeTrue();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        resolved.Should().Be(Path.TrimEndingDirectorySeparator(Path.Combine(home, suffix)));
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("'   '")]
    [InlineData("~anotheruser/settings")]
    [InlineData("\"unclosed")]
    [InlineData("'mismatched\"")]
    [InlineData("settings\nfolder")]
    [InlineData("\tsettings")]
    [InlineData("settings\0folder")]
    public void TryResolve_invalidInput_returnsInlineDiagnostic(string input)
    {
        PathResolver.TryResolve(input, _root, out var resolved, out var error).Should().BeFalse();

        resolved.Should().BeEmpty();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryResolve_shellExpressions_areLiteralFolderNames()
    {
        if (OperatingSystem.IsWindows()) return;

        const string input = "$HOME/$(touch should-not-exist)/`command`";

        PathResolver.TryResolve(input, _root, out var resolved, out _).Should().BeTrue();

        resolved.Should().Be(Path.Combine(_root, input));
        Directory.GetFileSystemEntries(_root).Should().BeEmpty();
    }

    [Fact]
    public void PathsEqual_caseAndTrailingSeparator_followHostPathRules()
    {
        PathResolver.PathsEqual(_root, _root + Path.DirectorySeparatorChar).Should().BeTrue();
        PathResolver.PathsEqual(Path.Combine(_root, "Case"), Path.Combine(_root, "case"))
            .Should().Be(OperatingSystem.IsWindows());
    }

    [Fact]
    public void ValidateTarget_readOnlyValidation_doesNotCreateMissingDirectories()
    {
        var target = Path.Combine(_root, "missing", "settings");

        PathResolver.ValidateTarget(target, probeWritable: false).Should().BeNull();

        Directory.GetFileSystemEntries(_root).Should().BeEmpty();
    }

    [Fact]
    public void ValidateTarget_newNestedTarget_removesProbeAndOnlyCreatedEmptyDirectories()
    {
        var existingEmptyParent = Path.Combine(_root, "existing-parent");
        Directory.CreateDirectory(existingEmptyParent);
        var target = Path.Combine(existingEmptyParent, "new", "nested", "settings");

        PathResolver.ValidateTarget(target).Should().BeNull();

        Directory.Exists(existingEmptyParent).Should().BeTrue();
        Directory.GetFileSystemEntries(existingEmptyParent).Should().BeEmpty();
    }

    [Fact]
    public void ValidateTarget_existingInstallation_preservesFilesAndEmptyDirectories()
    {
        var environmentFile = Path.Combine(_root, ".env");
        File.WriteAllText(environmentFile, "KEEP=synthetic");
        Directory.CreateDirectory(Path.Combine(_root, "empty-existing"));

        PathResolver.ValidateTarget(_root).Should().BeNull();

        File.ReadAllText(environmentFile).Should().Be("KEEP=synthetic");
        Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).Order()
            .Should().Equal(".env", "empty-existing");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateTarget_fileInPath_returnsDiagnosticWithoutChangingFile(bool asAncestor)
    {
        var file = Path.Combine(_root, "file");
        File.WriteAllText(file, "preserve");
        var target = asAncestor ? Path.Combine(file, "settings") : file;

        PathResolver.ValidateTarget(target).Should().Contain("is a file");

        File.ReadAllText(file).Should().Be("preserve");
        Directory.GetFileSystemEntries(_root).Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateTarget_symbolicLinkAlias_isRejectedBeforeAnyMutation(bool asAncestor)
    {
        if (OperatingSystem.IsWindows()) return;

        var actual = Path.Combine(_root, "actual");
        Directory.CreateDirectory(actual);
        File.WriteAllText(Path.Combine(actual, ".env"), "existing installation");
        var alias = Path.Combine(_root, "alias");
        Directory.CreateSymbolicLink(alias, actual);
        var target = asAncestor ? Path.Combine(alias, "settings") : alias;

        var error = PathResolver.ValidateTarget(target);

        error.Should().Contain("symbolic link or junction").And.Contain(alias);
        File.ReadAllText(Path.Combine(actual, ".env")).Should().Be("existing installation");
        Directory.GetFileSystemEntries(actual).Should().ContainSingle();
    }

    [Fact]
    public void ValidateTarget_danglingSymbolicLink_isRejectedWithoutCreatingItsDestination()
    {
        if (OperatingSystem.IsWindows()) return;

        var missing = Path.Combine(_root, "missing");
        var alias = Path.Combine(_root, "alias");
        Directory.CreateSymbolicLink(alias, missing);

        PathResolver.ValidateTarget(alias).Should().Contain("symbolic link or junction");

        Directory.Exists(missing).Should().BeFalse();
    }

    [Fact]
    public void ValidateTarget_permissionFailure_returnsDiagnosticAndPreservesExistingDirectory()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root") return;

        var restricted = Path.Combine(_root, "restricted");
        Directory.CreateDirectory(restricted);
        File.SetUnixFileMode(restricted, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            PathResolver.ValidateTarget(Path.Combine(restricted, "new", "settings"))
                .Should().Contain("Cannot write to installation folder");

            Directory.GetFileSystemEntries(restricted).Should().BeEmpty();
        }
        finally
        {
            File.SetUnixFileMode(restricted, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void ValidateSource_markerFiles_doNotCountAsSourceDirectories()
    {
        TargetDirScreenTests.CreateSource(_root);
        Directory.Delete(Path.Combine(_root, "Backend"), recursive: true);
        Directory.Delete(Path.Combine(_root, "Frontend"), recursive: true);
        File.WriteAllText(Path.Combine(_root, "Backend"), "marker");
        File.WriteAllText(Path.Combine(_root, "Frontend"), "marker");

        SourceAssetValidator.Validate(_root, new FeatureSelection())
            .Should().Contain("Backend/Alexandria.Api/Dockerfile");
    }

    [Theory]
    [InlineData("scripts/garage-init/init.sh")]
    [InlineData("scripts/postgres/init/01-create-exporter-user.sh")]
    [InlineData("rabbitmq/definitions.template.json")]
    [InlineData("nginx/nginx.conf")]
    [InlineData("classificators/essentia/models/models.sha256")]
    public void ValidateSource_missingRequiredAsset_namesExactAsset(string relativePath)
    {
        TargetDirScreenTests.CreateSource(_root);
        File.Delete(Path.Combine(_root, relativePath));

        SourceAssetValidator.Validate(_root, FeatureSelection.FromDefaults())
            .Should().Contain(relativePath);
    }

    [Fact]
    public void ValidateSource_disabledWorker_doesNotRequireItsDockerfile()
    {
        TargetDirScreenTests.CreateSource(_root);
        File.Delete(Path.Combine(_root, "Backend/Alexandria.Workers.Document/Dockerfile"));
        var features = new FeatureSelection();

        SourceAssetValidator.Validate(_root, features).Should().BeNull();

        features.SetEnabled(FeatureCatalog.DocumentPreviews.Id, true);

        SourceAssetValidator.Validate(_root, features).Should().Contain("Alexandria.Workers.Document/Dockerfile");
    }

    [Fact]
    public void ValidateSource_missingShippedTemplate_namesTemplateOutsideSource()
    {
        TargetDirScreenTests.CreateSource(_root);
        var missingTemplates = Path.Combine(_root, "missing-templates");

        SourceAssetValidator.Validate(_root, new FeatureSelection(), missingTemplates)
            .Should().Contain("installer template").And.Contain("base.yml.template");

        Directory.Exists(missingTemplates).Should().BeFalse();
    }

    [Fact]
    public void ValidateSource_readOnlyCheckout_readsWithoutCreatingFiles()
    {
        if (OperatingSystem.IsWindows()) return;

        TargetDirScreenTests.CreateSource(_root);
        var before = Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories).Order().ToArray();
        var directories = Directory.GetDirectories(_root, "*", SearchOption.AllDirectories).Append(_root).ToArray();

        foreach (var file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead);
        }

        foreach (var directory in directories)
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        try
        {
            SourceAssetValidator.Validate(_root, FeatureSelection.FromDefaults()).Should().BeNull();

            Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories).Order().Should().Equal(before);
        }
        finally
        {
            foreach (var directory in directories)
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
