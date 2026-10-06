using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Builder.Tests.Services;

public sealed class ComposeImageParityTests
{
    [Fact]
    public void ThirdPartyImages_matchReferenceVersionsAndHaveImmutableDigests()
    {
        var source = ConfigurationArtifactFixture.FindRepositoryRoot();
        var reference = Images(File.ReadAllText(Path.Combine(source, "docker-compose.local.yml")))
            .Select(Version).ToHashSet(StringComparer.Ordinal);
        var templates = Path.Combine(AppContext.BaseDirectory, "Templates", "Local", "docker-compose");
        var generated = Directory.EnumerateFiles(templates, "*.template")
            .SelectMany(path => Images(File.ReadAllText(path))).ToArray();

        generated.Should().HaveCount(10);

        foreach (var image in generated)
        {
            image.Should().MatchRegex("@sha256:[a-f0-9]{64}$");
            reference.Should().Contain(Version(image), "installer images must use the reference Compose versions");

            var pinnedReference = Images(File.ReadAllText(Path.Combine(source, "docker-compose.local.yml")))
                .Single(value => Version(value) == Version(image));

            if (pinnedReference.Contains('@')) image.Should().Be(pinnedReference);
        }
    }

    private static string Version(string image) => image.Split('@')[0];

    private static IEnumerable<string> Images(string compose) => Regex.Matches(compose, @"(?m)^\s+image:\s*(?<image>\S+)\s*$")
        .Select(match => match.Groups["image"].Value);
}
