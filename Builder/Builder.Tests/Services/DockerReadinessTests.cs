using System.Diagnostics;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public class DockerReadinessTests
{
    private sealed class FakeRunner(Func<ProcessStartInfo, ProcessResult> response) : IProcessRunner
    {
        public List<ProcessStartInfo> Calls { get; } = [];
        public List<TimeSpan> Timeouts { get; } = [];
        public bool? CapturedStdoutWasStreamed { get; private set; }

        public ProcessResult Run(ProcessStartInfo info, TimeSpan timeout, CancellationToken token,
            Action<string>? onStandardOutput = null, Action<string>? onStandardError = null, bool captureOutput = true)
        {
            token.ThrowIfCancellationRequested();
            Calls.Add(info);
            Timeouts.Add(timeout);
            CapturedStdoutWasStreamed = captureOutput && onStandardOutput is not null;
            var result = response(info);

            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)) onStandardOutput?.Invoke(line);
            foreach (var line in result.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries)) onStandardError?.Invoke(line);

            return result;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan duration) => _ticks += duration.Ticks;
    }

    private static DockerService CreateSut(IProcessRunner runner, CancellationToken token = default, TimeProvider? timeProvider = null)
    {
        var sut = new DockerService(runner, timeProvider: timeProvider) { PollInterval = TimeSpan.Zero };
        sut.Configure(new InstallationConfig { ProjectName = "test-project" }, token);
        return sut;
    }

    [Theory]
    [InlineData("[{\"Service\":\"api\",\"Name\":\"test-api\",\"State\":\"running\",\"Health\":\"healthy\",\"ExitCode\":0}]")]
    [InlineData("{\"Service\":\"api\",\"Name\":\"test-api\",\"State\":\"running\",\"Health\":\"healthy\",\"ExitCode\":\"0\"}\n")]
    public void ParseStatuses_arrayOrJsonLines_readsStructuredFields(string json)
    {
        var statuses = DockerService.ParseStatuses(json);

        statuses.Should().ContainSingle().Which.Should().Be(new ContainerStatus("api", "test-api", "running", "healthy", 0));
    }

    [Theory]
    [InlineData("{\"State\":\"running\"}")]
    [InlineData("{\"Service\":\"api\"}")]
    [InlineData("{\"Service\":\"api\",\"State\":\"exited\",\"ExitCode\":false}")]
    public void ParseStatuses_incompleteOrInvalidRows_rejectsAmbiguousEvidence(string json)
    {
        Action parse = () => DockerService.ParseStatuses(json);

        parse.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("running", "unhealthy", 0, true)]
    [InlineData("running", "starting", 0, false)]
    [InlineData("restarting", "", 1, false)]
    [InlineData("dead", "", 1, true)]
    [InlineData("exited", "", 0, true)]
    [InlineData("exited", "", 3, true)]
    public void Evaluate_longRunningServiceNotReady_neverReportsSuccess(string state, string health, int exitCode, bool failed)
    {
        var status = new ContainerStatus("api", "init-looking-name", state, health, exitCode);

        var result = ContainerReadiness.Evaluate(["api"], [status], ["init"]);

        result.Ready.Should().BeFalse();
        result.Failed.Should().Be(failed);
    }

    [Fact]
    public void Evaluate_missingSelectedService_rejectsHealthySubset()
    {
        var result = ContainerReadiness.Evaluate(["api", "document-worker"],
            [new ContainerStatus("api", "api", "running", "healthy", 0)], []);

        result.Ready.Should().BeFalse();
        result.Failed.Should().BeTrue();
        result.Problems.Should().Contain("document-worker: container missing");
    }

    [Fact]
    public void Evaluate_oneShotMustExitZero_usesExactServiceIdentifier()
    {
        var completed = new ContainerStatus("garage-init", "anything", "exited", "", 0);
        ContainerReadiness.Evaluate(["garage-init"], [completed], ["garage-init"]).Ready.Should().BeTrue();
        ContainerReadiness.Evaluate(["garage-init"], [completed with { ExitCode = null }], ["garage-init"]).Ready.Should().BeFalse();
        ContainerReadiness.Evaluate(["garage-init"], [completed with { ExitCode = 1 }], ["garage-init"]).Failed.Should().BeTrue();
        ContainerReadiness.Evaluate(["garage-init"], [completed with { State = "running" }], ["garage-init"]).Ready.Should().BeFalse();
        ContainerReadiness.Evaluate(["garage-init"], [completed], ["init"]).Ready.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_multipleReplicas_rejectsUnexpectedScaleAndQualifiesUnprobedWorker()
    {
        var statuses = new[]
        {
            new ContainerStatus("api", "api-1", "running", "healthy", 0),
            new ContainerStatus("api", "api-2", "running", "healthy", 0),
            new ContainerStatus("worker", "worker-1", "running", "", 0),
        };
        var result = ContainerReadiness.Evaluate(["api", "worker"], statuses, []);

        result.Ready.Should().BeFalse();
        result.Failed.Should().BeTrue();
        result.Problems.Should().Contain("api: expected 1 container, found 2");
        ContainerReadiness.Evaluate(["api", "worker"], [statuses[0], statuses[2]], []).Ready.Should().BeTrue();
        statuses[2].Display.Should().Contain("no healthcheck; process state only");
        ContainerReadiness.Evaluate(["api", "worker"], statuses.Append(statuses[0] with { Health = "unhealthy" }).ToList(), [])
            .Ready.Should().BeFalse();
        ContainerReadiness.Evaluate([], statuses, []).Ready.Should().BeFalse();
    }

    [Fact]
    public void GetServiceStatuses_includesStoppedContainersAndProjectIdentity_withoutStreamingJson()
    {
        var runner = new FakeRunner(_ => new ProcessResult(0,
            "{\"Service\":\"api\",\"State\":\"exited\",\"Health\":\"\",\"ExitCode\":7}", ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);
        var output = new List<string>();

        sut.GetServiceStatuses(Path.GetTempPath(), output.Add)[0].ExitCode.Should().Be(7);

        runner.Calls[0].ArgumentList.Should().ContainInOrder("compose", "--project-name", "test-project");
        runner.Calls[0].ArgumentList.TakeLast(4).Should().Equal("ps", "--all", "--format", "json");
        runner.CapturedStdoutWasStreamed.Should().BeFalse();
        output.Should().NotContain(line => line.Contains("ExitCode", StringComparison.Ordinal));
    }

    [Fact]
    public void GetExpectedServices_readsSelectedComposeInventory_withoutResolvingEnvironmentToLogs()
    {
        var runner = new FakeRunner(_ => new ProcessResult(0, "api\npostgres\ngarage-init\n", ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);

        sut.GetExpectedServices(Path.GetTempPath()).Should().Equal("api", "postgres", "garage-init");

        runner.Calls[0].ArgumentList.TakeLast(2).Should().Equal("config", "--services");
        runner.CapturedStdoutWasStreamed.Should().BeFalse();
    }

    [Fact]
    public void ComposeUpCore_startsOnlyCatalogInfrastructure()
    {
        var runner = new FakeRunner(_ => new ProcessResult(0, "", ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);

        sut.ComposeUpCore(Path.GetTempPath()).Should().BeTrue();

        runner.Calls.Should().ContainSingle();
        var arguments = runner.Calls[0].ArgumentList.ToArray();
        var start = Array.IndexOf(arguments, "up");
        arguments.Skip(start + 2).Should().BeEquivalentTo("postgres", "garage", "garage-init", "rabbitmq-config", "rabbitmq");
    }

    [Fact]
    public void WaitForServices_unhealthyService_failsImmediatelyWithoutWaiting()
    {
        var runner = new FakeRunner(_ => new ProcessResult(0,
            "{\"Service\":\"api\",\"State\":\"running\",\"Health\":\"unhealthy\",\"ExitCode\":0}", ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);

        sut.WaitForServices(Path.GetTempPath(), ["api"], TimeSpan.FromMinutes(5), []).Should().BeFalse();

        runner.Calls.Should().ContainSingle();
    }

    [Fact]
    public void ComposeBuild_cancelledSession_propagatesBeforeStartingChild()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runner = new FakeRunner(_ => throw new InvalidOperationException("must not run"));
        var sut = CreateSut(runner, cancellation.Token);

        Action build = () => sut.ComposeBuild(Path.GetTempPath());

        build.Should().Throw<OperationCanceledException>();
        runner.Calls.Should().BeEmpty();
    }

    [Fact]
    public void WaitForMonitoring_retriesPendingEndpointAndUsesOnlyInternalComposeProbes()
    {
        var lokiAttempts = 0;
        var runner = new FakeRunner(info =>
        {
            var url = info.ArgumentList.Last();
            if (url == "http://loki:3100/ready" && ++lokiAttempts == 1)
                return new ProcessResult(1, "", "not ready");

            return new ProcessResult(0, "ready", "");
        });
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);

        sut.WaitForMonitoring(Path.GetTempPath(), TimeSpan.FromSeconds(1)).Should().BeTrue();

        lokiAttempts.Should().Be(2);
        runner.Calls.Should().HaveCount(5);
        runner.Calls.Should().OnlyContain(info => info.ArgumentList.TakeLast(6)
            .Take(5).SequenceEqual(new[] { "exec", "-T", "grafana", "wget", "-qO-" }));
        runner.Calls.Select(info => info.ArgumentList.Last()).Should().Contain("http://grafana:3000/monitoring/api/health");
    }

    [Fact]
    public void WaitForMonitoring_timedOutEndpointIsNotReportedReady()
    {
        var clock = new ManualTimeProvider();
        var runner = new FakeRunner(_ =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return new ProcessResult(-1, "", "unavailable", TimedOut: true);
        });
        var sut = CreateSut(runner, TestContext.Current.CancellationToken, clock);
        var output = new List<string>();

        sut.WaitForMonitoring(Path.GetTempPath(), TimeSpan.FromSeconds(1), output.Add).Should().BeFalse();

        runner.Calls.Should().ContainSingle();
        runner.Timeouts.Should().Equal(TimeSpan.FromSeconds(1));
        output.Should().Contain(line => line.Contains("Monitoring readiness is incomplete", StringComparison.Ordinal));
        output.Should().Contain(line => line.Contains("1.0s", StringComparison.Ordinal) && line.Contains("command timed out", StringComparison.Ordinal));
        output.Should().NotContain(line => line.StartsWith("Ready:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(7, false, "exit 7")]
    [InlineData(-1, true, "timed out")]
    public void WaitForServices_statusCommandFails_stopsWithElapsedFailure(int exitCode, bool timedOut, string diagnostic)
    {
        var runner = new FakeRunner(_ => new ProcessResult(exitCode, "", "daemon error", timedOut));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);
        var output = new List<string>();

        sut.WaitForServices(Path.GetTempPath(), ["api"], TimeSpan.FromMinutes(5), [], onOutput: output.Add).Should().BeFalse();

        runner.Calls.Should().ContainSingle();
        output.Should().Contain(line => line.Contains("readiness failed after", StringComparison.Ordinal) &&
                                       line.Contains(diagnostic, StringComparison.Ordinal) && line.Contains("daemon error", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("garbled status")]
    public void WaitForServices_emptyOrInvalidStatus_stopsWithoutReportingSuccess(string status)
    {
        var runner = new FakeRunner(_ => new ProcessResult(0, status, ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);
        var output = new List<string>();

        sut.WaitForServices(Path.GetTempPath(), ["api"], TimeSpan.FromMinutes(5), [], onOutput: output.Add).Should().BeFalse();

        runner.Calls.Should().ContainSingle();
        output.Should().NotBeEmpty();
    }

    [Fact]
    public void WaitForServices_healthStarting_retriesWithinOneSharedDeadlineAndReportsBlockingService()
    {
        var clock = new ManualTimeProvider();
        var runner = new FakeRunner(_ =>
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            return new ProcessResult(0, "{\"Service\":\"api\",\"State\":\"running\",\"Health\":\"starting\"}", "");
        });
        var sut = CreateSut(runner, TestContext.Current.CancellationToken, clock);
        var output = new List<string>();

        sut.WaitForServices(Path.GetTempPath(), ["api"], TimeSpan.FromSeconds(3), [], onOutput: output.Add).Should().BeFalse();

        runner.Timeouts.Should().Equal(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        output.Last().Should().Contain("timed out after 3.0s").And.Contain("api: running (starting)");
    }

    [Fact]
    public void WaitForServices_healthStartingThenHealthy_completesWithoutRejectingUnrelatedRows()
    {
        var polls = 0;
        var runner = new FakeRunner(_ => new ProcessResult(0,
            "[{\"Service\":\"api\",\"State\":\"running\",\"Health\":\"" + (++polls == 1 ? "starting" : "healthy") +
            "\"},{\"Service\":\"orphan\",\"State\":\"dead\"}]", ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);

        sut.WaitForServices(Path.GetTempPath(), ["api"], TimeSpan.FromSeconds(10), []).Should().BeTrue();

        runner.Calls.Should().HaveCount(2);
    }

    [Fact]
    public void WaitForHealthy_inventoryLookup_consumesTheSameOverallDeadline()
    {
        var clock = new ManualTimeProvider();
        var runner = new FakeRunner(info =>
        {
            if (info.ArgumentList.Contains("--services"))
            {
                clock.Advance(TimeSpan.FromSeconds(2));
                return new ProcessResult(0, "api", "");
            }

            clock.Advance(TimeSpan.FromSeconds(1));
            return new ProcessResult(0, "{\"Service\":\"api\",\"State\":\"running\",\"Health\":\"starting\"}", "");
        });
        var sut = CreateSut(runner, TestContext.Current.CancellationToken, clock);

        sut.WaitForHealthy(Path.GetTempPath(), TimeSpan.FromSeconds(3), []).Should().BeFalse();

        runner.Timeouts.Should().Equal(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Evaluate_orphanWithMatchingContainerName_cannotSatisfyAnExpectedService()
    {
        var result = ContainerReadiness.Evaluate(["api"], [new ContainerStatus("orphan", "api", "running", "healthy", 0)], []);

        result.Ready.Should().BeFalse();
        result.Failed.Should().BeTrue();
        result.Problems.Should().Contain("api: container missing");
    }

    [Fact]
    public void WaitForServices_emptyExpectedInventory_refusesBeforeInspectingDocker()
    {
        var runner = new FakeRunner(_ => throw new InvalidOperationException("must not run"));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);
        var output = new List<string>();

        sut.WaitForServices(Path.GetTempPath(), [], TimeSpan.FromSeconds(10), [], onOutput: output.Add).Should().BeFalse();

        runner.Calls.Should().BeEmpty();
        output.Should().ContainSingle().Which.Should().Contain("inventory is empty");
    }

    [Fact]
    public void ConfigureDiagnostics_capturedQueries_reportOnlyCommandAndExitMetadata()
    {
        var runner = new FakeRunner(_ => new ProcessResult(0, "api\n", "sensitive-in-memory-diagnostic"));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);
        var calls = new List<(ProcessStartInfo Info, ProcessResult? Result)>();
        sut.ConfigureDiagnostics((info, result) => calls.Add((info, result)));

        sut.GetExpectedServices(Path.GetTempPath()).Should().Equal("api");

        calls.Should().HaveCount(2);
        calls[0].Result.Should().BeNull();
        calls[0].Info.ArgumentList.TakeLast(2).Should().Equal("config", "--services");
        calls[0].Info.Environment.Should().BeEmpty();
        calls[1].Result!.Succeeded.Should().BeTrue();
        calls[1].Result!.StandardOutput.Should().BeEmpty();
        calls[1].Result!.StandardError.Should().BeEmpty();
    }

    [Fact]
    public void GetServiceLogs_selectedService_scopesAndRedactsItsEvidence()
    {
        const string secret = "synthetic-admin-credential";
        var runner = new FakeRunner(info => info.ArgumentList.Contains("logs")
            ? new ProcessResult(0, "api | password=" + secret + "\napi | ready", "api | amqp://user:" + secret + "@rabbitmq")
            : new ProcessResult(0, "api\ngarage-init", ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);
        sut.Configure(new InstallationConfig { ProjectName = "test-project", Credentials = new() { ["ADMIN_PASSWORD"] = secret } }, TestContext.Current.CancellationToken);

        var logs = sut.GetServiceLogs(Path.GetTempPath(), "api", 25);

        logs.Should().Contain("api | ready");
        logs.Should().OnlyContain(line => !line.Contains(secret, StringComparison.Ordinal));
        runner.Calls.Last().ArgumentList.TakeLast(5).Should().Equal("logs", "--no-color", "--tail", "25", "api");
    }

    [Theory]
    [InlineData("api-other")]
    [InlineData("--all")]
    public void GetServiceLogs_nonSelectedOrOptionLikeName_refusesBeforeStartingLogs(string service)
    {
        var runner = new FakeRunner(_ => new ProcessResult(0, "api", ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);

        Action read = () => sut.GetServiceLogs(Path.GetTempPath(), service);

        read.Should().Throw<ArgumentException>().WithMessage("*exact service name*");
        runner.Calls.Should().ContainSingle();
    }

    [Fact]
    public void WaitForMonitoring_cancellationStopsWithoutStartingProbes()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runner = new FakeRunner(_ => throw new InvalidOperationException("must not run"));
        var sut = CreateSut(runner, cancellation.Token);

        var wait = () => sut.WaitForMonitoring(Path.GetTempPath(), TimeSpan.FromMinutes(5));

        wait.Should().Throw<OperationCanceledException>();
        runner.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{\"443/tcp\":[{\"HostIp\":\"0.0.0.0\",\"HostPort\":\"8443\"}]}", true)]
    [InlineData("{\"443/tcp\":[{\"HostIp\":\"0.0.0.0\",\"HostPort\":\"9443\"}]}", false)]
    [InlineData("{\"80/tcp\":[{\"HostIp\":\"0.0.0.0\",\"HostPort\":\"8443\"}]}", false)]
    [InlineData("{\"443/tcp\":null}", false)]
    public void IsHttpsPortOwned_inspectsOnlyThisProjectsNginxBinding(string bindings, bool owned)
    {
        var runner = new FakeRunner(info => new ProcessResult(0,
            info.ArgumentList[0] == "ps" ? "owned-nginx-id\n" : bindings, ""));
        var sut = CreateSut(runner, TestContext.Current.CancellationToken);

        sut.IsHttpsPortOwned(Path.GetTempPath(), 8443).Should().Be(owned);

        runner.Calls[0].ArgumentList.Should().Contain("label=com.docker.compose.project=test-project");
        runner.Calls[0].ArgumentList.Should().Contain("label=com.docker.compose.service=nginx");
        runner.Calls[1].ArgumentList.Should().Contain("{{json .NetworkSettings.Ports}}");
    }

    [Fact]
    public void ComposeBuild_withoutPersistedIdentity_refusesCommand()
    {
        var runner = new FakeRunner(_ => throw new InvalidOperationException("must not run"));
        var sut = new DockerService(runner);

        Action build = () => sut.ComposeBuild(Path.GetTempPath());

        build.Should().Throw<InvalidOperationException>().WithMessage("*persisted*identity*");
        runner.Calls.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfiguration_parentEnvironmentContainsSecret_doesNotOverrideReviewedFile()
    {
        const string key = "BUILDER_SYNTHETIC_PASSWORD";
        var priorValue = Environment.GetEnvironmentVariable(key);
        var directory = Path.Combine(Path.GetTempPath(), "builder-compose-parent-env", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            Environment.SetEnvironmentVariable(key, "incorrect-parent-value");
            EnvGenerator.WriteFile(Path.Combine(directory, ".env"), new Dictionary<string, string> { [key] = "reviewed-file-value" });
            var runner = new FakeRunner(_ => new ProcessResult(0, "", ""));
            var sut = CreateSut(runner, TestContext.Current.CancellationToken);

            sut.ValidateConfiguration(directory).Should().BeTrue();

            runner.Calls.Should().ContainSingle();
            runner.Calls[0].Environment.Should().NotContainKey(key);
            File.ReadAllText(Path.Combine(directory, ".env")).Should().Contain("reviewed-file-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, priorValue);
            Directory.Delete(directory, recursive: true);
        }
    }
}
