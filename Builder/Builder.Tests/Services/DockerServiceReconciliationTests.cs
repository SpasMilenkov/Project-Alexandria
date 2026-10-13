using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public class DockerServiceReconciliationTests
{
    private static readonly string SelectedId = new('a', 64);
    private static readonly string DisabledWorkerId = new('b', 64);
    private static readonly string DisabledMonitoringId = new('c', 64);
    private static readonly string UnrelatedId = new('d', 64);

    [Fact]
    public void ComposeUpAll_disabledKnownServices_removesOnlyOwnedContainersAndPreservesAllVolumes()
    {
        using var fixture = new ReconciliationFixture();
        fixture.Containers.Add(new(SelectedId, "api"));
        fixture.Containers.Add(new(DisabledWorkerId, "document-worker", Volume: "document_worker_scratch"));
        fixture.Containers.Add(new(DisabledMonitoringId, "prometheus", Volume: "prometheus_local_data"));
        fixture.Containers.Add(new(UnrelatedId, "document-worker", Project: "unrelated-project", Target: Path.Combine(fixture.Target, "other")));
        var output = new List<string>();

        var result = fixture.Service.ComposeUpAll(fixture.Target, output.Add);

        result.Should().BeTrue();
        fixture.Mutations.Should().HaveCount(3);
        fixture.Mutations[0].Should().Equal("stop", "--time", "30", "--", DisabledWorkerId, DisabledMonitoringId);
        fixture.Mutations[1].Should().Equal("rm", "--", DisabledWorkerId, DisabledMonitoringId);
        fixture.Mutations[2].TakeLast(2).Should().Equal("up", "-d");
        fixture.Mutations.SelectMany(arguments => arguments).Should().NotContain(SelectedId).And.NotContain(UnrelatedId)
            .And.NotContain("--volumes").And.NotContain("-v").And.NotContain("--remove-orphans").And.NotContain("--force");
        fixture.Calls.Where(arguments => arguments[0] == "volume").Should()
            .OnlyContain(arguments => arguments[1] == "ls" || arguments[1] == "inspect");
        output.Should().Contain(line => line.Contains("Their data volumes are preserved", StringComparison.Ordinal));
    }

    [Fact]
    public void ComposeUpAll_unchangedSelection_doesNotStopOrRemoveExistingContainers()
    {
        using var fixture = new ReconciliationFixture();
        fixture.Config.Features.SetEnabled(FeatureCatalog.DocumentPreviews.Id, true);
        fixture.Containers.Add(new(SelectedId, "api"));
        fixture.Containers.Add(new(DisabledWorkerId, "document-worker", Volume: "document_worker_scratch"));

        fixture.Service.ComposeUpAll(fixture.Target).Should().BeTrue();

        fixture.Mutations.Should().ContainSingle();
        fixture.Mutations[0].TakeLast(2).Should().Equal("up", "-d");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComposeUpAll_conflictingProjectOrTarget_rejectsBeforeAnyMutation(bool wrongProject)
    {
        using var fixture = new ReconciliationFixture();
        fixture.Containers.Add(new(DisabledWorkerId, "document-worker",
            Project: wrongProject ? "different-project" : null,
            Target: wrongProject ? null : Path.Combine(fixture.Target, "different-target")));

        var action = () => fixture.Service.ComposeUpAll(fixture.Target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*labels conflict*");
        fixture.Mutations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("custom-sidecar")]
    [InlineData("")]
    public void ComposeUpAll_unknownServiceAmongDisabledCandidates_rejectsWholePlanBeforeAnyMutation(string service)
    {
        using var fixture = new ReconciliationFixture();
        fixture.Containers.Add(new(DisabledWorkerId, "document-worker"));
        fixture.Containers.Add(new(UnrelatedId, service));

        var action = () => fixture.Service.ComposeUpAll(fixture.Target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*unknown service*will not remove arbitrary containers*");
        fixture.Mutations.Should().BeEmpty();
    }

    [Fact]
    public void ComposeUpAll_manuallyStartedOneOff_rejectsBeforeAnyMutation()
    {
        using var fixture = new ReconciliationFixture();
        fixture.Containers.Add(new(DisabledWorkerId, "document-worker"));
        fixture.Containers.Add(new(UnrelatedId, "lyrics-worker", OneOff: true));

        var action = () => fixture.Service.ComposeUpAll(fixture.Target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*one-off container*will not remove manually started containers*");
        fixture.Mutations.Should().BeEmpty();
    }

    [Fact]
    public void ComposeUpAll_unownedVolumeAttachment_rejectsBeforeAnyMutation()
    {
        using var fixture = new ReconciliationFixture();
        fixture.Containers.Add(new(DisabledWorkerId, "document-worker", Volume: "external-data"));

        var action = () => fixture.Service.ComposeUpAll(fixture.Target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*without matching project ownership*");
        fixture.Mutations.Should().BeEmpty();
    }

    [Fact]
    public void ComposeUpAll_disabledContainerIdentityIncomplete_rejectsBeforeAnyMutation()
    {
        using var fixture = new ReconciliationFixture();
        fixture.Containers.Add(new("short-id", "document-worker"));

        var action = () => fixture.Service.ComposeUpAll(fixture.Target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*exact container identity*");
        fixture.Mutations.Should().BeEmpty();
    }

    [Fact]
    public void ComposeUpAll_inventoryChangedAfterReview_rejectsBeforeOwnershipInspectionOrMutation()
    {
        using var fixture = new ReconciliationFixture();
        fixture.Containers.Add(new(DisabledWorkerId, "document-worker"));
        fixture.InventoryOverride = ServiceCatalog.Resolve(fixture.Config.Features).ServiceIds.Append("document-worker").ToArray();

        var action = () => fixture.Service.ComposeUpAll(fixture.Target);

        action.Should().Throw<InvalidOperationException>().WithMessage("*differs from the reviewed feature selection*");
        fixture.Calls.Should().ContainSingle();
        fixture.Mutations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("stop", 1)]
    [InlineData("rm", 2)]
    public void ComposeUpAll_reconciliationCommandFails_doesNotStartServices(string failingCommand, int mutationCount)
    {
        using var fixture = new ReconciliationFixture();
        fixture.Containers.Add(new(DisabledWorkerId, "document-worker"));
        fixture.FailingCommand = failingCommand;

        fixture.Service.ComposeUpAll(fixture.Target).Should().BeFalse();

        fixture.Mutations.Should().HaveCount(mutationCount);
        fixture.Mutations.Should().NotContain(arguments => arguments[0] == "compose");
        fixture.Mutations.SelectMany(arguments => arguments).Should().NotContain("--volumes").And.NotContain("--force");
    }

    private sealed record ContainerSpec(string Id, string Service, string? Project = null, string? Target = null,
        bool OneOff = false, string? Volume = null);

    private sealed class ReconciliationFixture : IProcessRunner, IDisposable
    {
        private static readonly string[] VolumeKeys =
        [
            "postgres_local_data", "rabbitmq_local_data", "garage_local_meta_data", "garage_local_storage_data",
            "document_worker_scratch", "prometheus_local_data",
        ];

        public string Target { get; } = Directory.CreateTempSubdirectory("builder reconciliation ").FullName;
        public InstallationConfig Config { get; } = new() { ProjectName = "reviewed-project", Stage = "api-ready" };
        public DockerService Service { get; }
        public List<ContainerSpec> Containers { get; } = [];
        public List<string[]> Calls { get; } = [];
        public IReadOnlyList<string>? InventoryOverride { get; set; }
        public string? FailingCommand { get; set; }
        public List<string[]> Mutations => Calls.Where(arguments => arguments[0] is "stop" or "rm" ||
            arguments[0] == "compose" && arguments.Contains("up", StringComparer.Ordinal)).ToList();

        public ReconciliationFixture()
        {
            Service = new DockerService(this);
            Service.Configure(Config);
        }

        public ProcessResult Run(ProcessStartInfo info, TimeSpan timeout, CancellationToken cancellationToken,
            Action<string>? onStandardOutput = null, Action<string>? onStandardError = null, bool captureOutput = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arguments = info.ArgumentList.ToArray();
            Calls.Add(arguments);

            if (arguments[0] == FailingCommand) return new ProcessResult(1, string.Empty, "simulated reconciliation failure");

            var output = arguments[0] switch
            {
                "compose" when arguments[^1] == "--services" => string.Join('\n', InventoryOverride ?? ServiceCatalog.Resolve(Config.Features).ServiceIds),
                "compose" when arguments[^2] == "up" => string.Empty,
                "ps" => string.Join('\n', Containers.Select(container => container.Id)),
                "inspect" => ContainerJson(),
                "volume" when arguments[1] == "ls" => string.Join('\n', VolumeKeys.Select(VolumeName)),
                "volume" when arguments[1] == "inspect" => VolumeJson(),
                "stop" or "rm" => string.Empty,
                _ => throw new InvalidOperationException("Unexpected Docker command in reconciliation test"),
            };

            return new ProcessResult(0, output, string.Empty);
        }

        private string ContainerJson() => JsonSerializer.Serialize(Containers.Select(container => new
        {
            container.Id,
            Labels = new Dictionary<string, string>
            {
                ["com.docker.compose.project"] = container.Project ?? Config.ProjectName,
                ["com.docker.compose.service"] = container.Service,
                ["com.docker.compose.project.working_dir"] = container.Target ?? Target,
                ["com.docker.compose.project.config_files"] = Path.Combine(container.Target ?? Target, "docker-compose.yml"),
                ["com.docker.compose.oneoff"] = container.OneOff.ToString(),
            },
            Mounts = container.Volume is null ? [] : new[]
            {
                new { Type = "volume", Name = VolumeName(container.Volume), Destination = "/data" },
            },
        }), new JsonSerializerOptions());

        private string VolumeJson() => JsonSerializer.Serialize(VolumeKeys.Select(key => new
        {
            Name = VolumeName(key),
            Labels = new Dictionary<string, string>
            {
                ["com.docker.compose.project"] = Config.ProjectName,
                ["com.docker.compose.volume"] = key,
            },
        }), new JsonSerializerOptions());

        private string VolumeName(string key) => Config.ProjectName + "_" + key;

        public void Dispose() => Directory.Delete(Target, recursive: true);
    }
}
