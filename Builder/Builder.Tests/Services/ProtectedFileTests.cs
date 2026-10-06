using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Services;

public class ProtectedFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "builder-protected-tests", Guid.NewGuid().ToString("N"));

    public ProtectedFileTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void WriteAtomic_success_replacesContentWithRestrictedPermissions()
    {
        var path = Path.Combine(_directory, ".env");
        File.WriteAllText(path, "old");

        ProtectedFile.WriteAtomic(path, "new café 😀");

        File.ReadAllText(path).Should().Be("new café 😀");
        Directory.GetFiles(_directory).Should().ContainSingle();

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void WriteAtomic_interruptedBeforeReplace_retainsOriginalAndCleansRestrictedStaging()
    {
        var path = Path.Combine(_directory, ".env");
        File.WriteAllText(path, "original-secret");

        var action = () => ProtectedFile.WriteAtomic(path, "replacement-secret", null, staging =>
        {
            Path.GetDirectoryName(staging).Should().Be(_directory);

            if (!OperatingSystem.IsWindows())
            {
                File.GetUnixFileMode(staging).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            throw new IOException("Simulated interruption");
        });

        action.Should().Throw<IOException>();
        File.ReadAllText(path).Should().Be("original-secret");
        Directory.GetFiles(_directory).Should().ContainSingle();
    }

    [Fact]
    public void WriteAtomic_corruptedStaging_failsReadbackAndRetainsOriginal()
    {
        var path = Path.Combine(_directory, ".env");
        File.WriteAllText(path, "original");

        var action = () => ProtectedFile.WriteAtomic(path, "replacement", null,
            staging => File.WriteAllText(staging, "corrupted"));

        action.Should().Throw<IOException>().WithMessage("*read-back*");
        File.ReadAllText(path).Should().Be("original");
        Directory.GetFiles(_directory).Should().ContainSingle();
    }

    [Fact]
    public void WriteAtomic_consumerMode_isAppliedAfterRestrictedCreation()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_directory, "token");
        var consumerMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;

        ProtectedFile.WriteAtomic(path, "token", consumerMode,
            staging =>
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.GetUnixFileMode(staging).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            });

        File.GetUnixFileMode(path).Should().Be(consumerMode);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
