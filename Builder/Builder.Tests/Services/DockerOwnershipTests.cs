using System.Text.Json;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public class DockerOwnershipTests
{
    private static readonly string[] CoreVolumes =
        ["postgres_local_data", "rabbitmq_local_data", "garage_local_meta_data", "garage_local_storage_data"];

    private static DockerOwnershipInspector CreateSut(
        string target, string project = "legacy-project", bool missingWorkingDirectory = false,
        bool missingCoreVolume = false, bool unownedMount = false, bool swappedDatabaseMount = false)
    {
        var labels = new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = project,
            ["com.docker.compose.service"] = "postgres",
            ["com.docker.compose.project.config_files"] = Path.Combine(target, "docker-compose.yml"),
        };

        if (!missingWorkingDirectory)
        {
            labels["com.docker.compose.project.working_dir"] = target;
        }

        var keys = missingCoreVolume ? CoreVolumes.Skip(1).ToArray() : CoreVolumes;
        var mounts = keys.Select(key => new
        {
            Type = "volume",
            Name = "physical-" + (swappedDatabaseMount && key == "postgres_local_data" ? "rabbitmq_local_data" : key),
            Destination = key == "postgres_local_data" ? "/var/lib/postgresql/data" : "/other/" + key,
        }).ToList();

        if (unownedMount)
        {
            mounts.Add(new { Type = "volume", Name = "unowned-data", Destination = "/unowned" });
        }

        var containers = JsonSerializer.Serialize(new[] { new { Labels = labels, Mounts = mounts } }, new JsonSerializerOptions());
        var volumes = JsonSerializer.Serialize(keys.Select(key => new
        {
            Name = "physical-" + key,
            Labels = new Dictionary<string, string>
            {
                ["com.docker.compose.project"] = project,
                ["com.docker.compose.volume"] = key,
            },
        }), new JsonSerializerOptions());

        return new DockerOwnershipInspector(arguments =>
        {
            var output = arguments[0] switch
            {
                "ps" => "container-id\n",
                "inspect" => containers,
                "volume" when arguments[1] == "ls" => string.Join('\n', keys.Select(key => "physical-" + key)),
                "volume" => volumes,
                _ => throw new InvalidOperationException("Unexpected mutation or query"),
            };
            return new ProcessResult(0, output, "");
        });
    }

    [Fact]
    public void Discover_provenLegacyLabels_retainsActualProjectAndPhysicalVolumeAttachments()
    {
        var target = Path.Combine(Path.GetTempPath(), "legacy settings with spaces");
        var sut = CreateSut(target);

        var identity = sut.Discover(target);

        identity.ProjectName.Should().Be("legacy-project");
        identity.VolumeNames.Should().HaveCount(4);
        identity.VolumeNames["postgres_local_data"].Should().Be("physical-postgres_local_data");
        identity.PreserveFixedContainerNames.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, false, "*labels conflict*")]
    [InlineData(false, true, false, "*cannot be mapped safely*")]
    [InlineData(false, false, true, "*without matching project ownership*")]
    public void Discover_incompleteOwnership_stopsBeforeAdoption(bool missingDirectory, bool missingCore, bool unownedMount, string message)
    {
        var target = Path.Combine(Path.GetTempPath(), "legacy-settings");
        var sut = CreateSut(target, missingWorkingDirectory: missingDirectory, missingCoreVolume: missingCore, unownedMount: unownedMount);

        Action discover = () => sut.Discover(target);

        discover.Should().Throw<InvalidOperationException>().WithMessage(message);
    }

    [Fact]
    public void Discover_noContainerEvidence_refusesGuessingIdentityFromTargetName()
    {
        var sut = new DockerOwnershipInspector(_ => new ProcessResult(0, "", ""));

        Action discover = () => sut.Discover(Path.Combine(Path.GetTempPath(), "legacy-settings"));

        discover.Should().Throw<InvalidOperationException>().WithMessage("*No Docker container labels prove*");
    }

    [Fact]
    public void Discover_databaseMountDiffersFromVolumeLabels_refusesReplacingActualDataAttachment()
    {
        var target = Path.Combine(Path.GetTempPath(), "legacy-settings");
        var sut = CreateSut(target, swappedDatabaseMount: true);

        Action discover = () => sut.Discover(target);

        discover.Should().Throw<InvalidOperationException>().WithMessage("*actual postgres_local_data container attachment*");
    }

    [Fact]
    public void Verify_savedIdentityRetainsPhysicalVolumes_acceptsExactMapping()
    {
        var target = Path.Combine(Path.GetTempPath(), "legacy-settings");
        var sut = CreateSut(target);
        var config = new InstallationConfig
        {
            ProjectName = "legacy-project",
            VolumeNames = CoreVolumes.ToDictionary(key => key, key => "physical-" + key),
        };

        Action verify = () => sut.Verify(target, config);

        verify.Should().NotThrow();
        config.VolumeNames["postgres_local_data"] = "different-empty-database";
        verify.Should().Throw<InvalidOperationException>().WithMessage("*does not match saved ownership*");
    }

    [Fact]
    public void Verify_targetAlreadyOwnedByAnotherProject_refusesNewIdentity()
    {
        var target = Path.Combine(Path.GetTempPath(), "legacy-settings");
        var sut = CreateSut(target);

        Action verify = () => sut.Verify(target, new InstallationConfig { ProjectName = "new-project" });

        verify.Should().Throw<InvalidOperationException>().WithMessage("*labels conflict*");
    }

    [Fact]
    public void Verify_savedProjectBelongsToAnotherTarget_refusesReuse()
    {
        var oldTarget = Path.Combine(Path.GetTempPath(), "old-settings");
        var sut = CreateSut(oldTarget);

        Action verify = () => sut.Verify(Path.Combine(Path.GetTempPath(), "new-settings"),
            new InstallationConfig { ProjectName = "legacy-project" });

        verify.Should().Throw<InvalidOperationException>().WithMessage("*labels conflict*");
    }

    [Theory]
    [InlineData("infrastructure-started")]
    [InlineData("storage-ready")]
    [InlineData("services-started")]
    [InlineData("complete")]
    public void Verify_establishedDefaultVolumesMissing_refusesEmptyReplacement(string stage)
    {
        var sut = new DockerOwnershipInspector(_ => new ProcessResult(0, "", ""));
        var target = Path.Combine(Path.GetTempPath(), "established-settings");
        var config = new InstallationConfig { ProjectName = "existing-project", Stage = stage };

        var action = () => sut.Verify(target, config);

        action.Should().Throw<InvalidOperationException>().WithMessage("*data volume is missing*empty replacement*");
    }

    [Fact]
    public void Verify_newInstallationWithoutVolumes_acceptsEmptyProject()
    {
        var sut = new DockerOwnershipInspector(_ => new ProcessResult(0, "", ""));
        var config = new InstallationConfig { ProjectName = "new-project" };

        var action = () => sut.Verify(Path.Combine(Path.GetTempPath(), "new-settings"), config);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(null, "fresh-generated", false)]
    [InlineData("established", "fresh-generated", false)]
    [InlineData("established", "established", true)]
    public void Verify_existingGrafanaVolume_requiresDurableMatchingCredentials(string? savedPassword, string effectivePassword, bool accepted)
    {
        var target = Path.Combine(Path.GetTempPath(), "builder-grafana-ownership", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(target);

        try
        {
            if (savedPassword is not null)
            {
                EnvGenerator.WriteFile(Path.Combine(target, ".env"), new Dictionary<string, string>
                {
                    ["GRAFANA_PASSWORD"] = savedPassword,
                });
            }

            var volume = JsonSerializer.Serialize(new[]
            {
                new
                {
                    Name = "existing-grafana",
                    Labels = new Dictionary<string, string>
                    {
                        ["com.docker.compose.project"] = "existing-project",
                        ["com.docker.compose.volume"] = "grafana_local_data",
                    },
                },
            });
            var sut = new DockerOwnershipInspector(arguments => new ProcessResult(0, arguments[0] switch
            {
                "ps" => "",
                "volume" when arguments[1] == "ls" => "existing-grafana\n",
                "volume" => volume,
                _ => throw new InvalidOperationException("Unexpected query"),
            }, ""));
            var config = new InstallationConfig
            {
                ProjectName = "existing-project",
                VolumeNames = new() { ["grafana_local_data"] = "existing-grafana" },
                EnvironmentValues = new() { ["GRAFANA_PASSWORD"] = effectivePassword },
            };

            var action = () => sut.Verify(target, config);

            if (accepted)
            {
                action.Should().NotThrow();
            }
            else
            {
                action.Should().Throw<InvalidOperationException>().WithMessage("*matching established password*will not replace*");
            }
        }
        finally
        {
            Directory.Delete(target, recursive: true);
        }
    }
}
