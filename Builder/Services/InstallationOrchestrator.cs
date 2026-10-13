using Builder.Models;
using Builder.Workflow;

namespace Builder.Services;

public sealed class InstallationOrchestrator(IConfigurationService configuration, IDockerService docker, ISystemChecker? systemChecker = null,
    IHttpsEndpointVerifier? httpsVerifier = null, IInstallationModelPreparation? modelPreparation = null,
    IAdminLoginVerifier? adminLoginVerifier = null)
{
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromMinutes(5);

    public void Execute(InstallationContext context, Action<string, Action<Action<string>>> runStage, Action? poll = null)
    {
        var config = context.Config;
        var capabilities = ServiceCatalog.Resolve(config.Features);
        var target = context.InstallPath;
        var token = context.Cancellation.Token;
        context.DeploymentVerified = false;
        context.AdminLoginVerified = false;
        docker.Configure(config, token);
        var checker = systemChecker ?? new SystemChecker();
        checker.ConfigureCancellation(token);

        runStage("Checking the supported platform", _ =>
        {
            token.ThrowIfCancellationRequested();
            var hostError = checker.GetHostPlatformError();

            if (!string.IsNullOrWhiteSpace(hostError)) throw new InvalidOperationException(hostError);

            var contextError = checker.GetDockerContextError();

            if (!string.IsNullOrWhiteSpace(contextError)) throw new InvalidOperationException(contextError);

            var daemonError = checker.GetDockerPlatformError();

            if (!string.IsNullOrWhiteSpace(daemonError)) throw new InvalidOperationException(daemonError);
        });

        runStage("Writing configuration files", _ =>
        {
            token.ThrowIfCancellationRequested();
            var pathError = PathResolver.ValidateTarget(target, probeWritable: false);
            if (pathError is not null) throw new InvalidOperationException(pathError);
            docker.VerifyInstallationOwnership(target, config);

            var environmentPath = Path.Combine(target, ".env");
            var currentEnvironment = File.Exists(environmentPath) ? File.ReadAllText(environmentPath) : null;

            if (currentEnvironment != context.ApprovedEnvironmentContent)
            {
                throw new InvalidOperationException("The environment file changed after review. Return to the summary and review the current settings before replacing any files.");
            }

            config.Stage = "configuration";
            configuration.BeginGeneration(config, target);
            EnvGenerator.WriteFile(environmentPath, config.EnvironmentValues);
            configuration.WriteAllConfigFiles(config, target);
        });

        runStage("Validating the deployment", output =>
        {
            Require(docker.ValidateConfiguration(target, output), "Compose rejected the generated configuration.");
            var actual = docker.GetExpectedServices(target, output);
            var missing = capabilities.ServiceIds.Except(actual, StringComparer.Ordinal).ToArray();
            var unexpected = actual.Except(capabilities.ServiceIds, StringComparer.Ordinal).ToArray();

            if (missing.Length > 0) throw new InvalidOperationException("Deployment is missing required services: " + string.Join(", ", missing));
            if (unexpected.Length > 0) throw new InvalidOperationException("Deployment includes services outside the reviewed selection: " + string.Join(", ", unexpected));
        });

        if (capabilities.AudioAnalysisEnabled)
        {
            runStage("Preparing and verifying audio models", output =>
                (modelPreparation ?? new InstallationModelPreparation()).Prepare(config, output, token));
        }

        runStage("Building Alexandria", output => Require(docker.ComposeBuild(target, output), "The image build failed. Retry with the same installation folder after resolving the reported build error."));

        runStage("Starting storage and messaging", output =>
        {
            context.DeploymentStarted = true;
            Require(docker.ComposeUpCore(target, output), "Infrastructure could not start.");
            Require(docker.WaitForServices(target, capabilities.InfrastructureServiceIds, ReadinessTimeout, capabilities.OneShotServiceIds,
                _ => poll?.Invoke(), output), "Infrastructure initialization did not complete successfully.");
            var actualCapacity = StorageCapacityReceipt.Commit(context);
            output($"Garage layout capacity: {actualCapacity / 1_000_000_000.0:0.###} GB; existing data preserved.");
            SaveStage("infrastructure-started");
        });

        runStage("Persisting storage keys", output =>
        {
            PersistStorageKeys(context, output);
            SaveStage("storage-ready");
        });

        runStage("Initializing the database and admin account", output =>
        {
            Require(docker.ComposeUpApi(target, output), "The API could not start.");
            Require(docker.WaitForServices(target, ["api"], ReadinessTimeout, [],
                _ => poll?.Invoke(), output), "Database, admin bootstrap, or API readiness failed.");
            SaveStage("api-ready");
        });

        runStage("Starting Alexandria services", output =>
        {
            docker.VerifyInstallationOwnership(target, config);

            if (!checker.IsPortAvailable(config.PublicEndpoint.HttpsPort) &&
                !docker.IsHttpsPortOwned(target, config.PublicEndpoint.HttpsPort))
            {
                throw new InvalidOperationException($"HTTPS port {config.PublicEndpoint.HttpsPort} became occupied by another service. " +
                    "Existing services were preserved. Return to network access and choose a free port before retrying.");
            }

            Require(docker.ComposeUpAll(target, output), "Alexandria services could not start.");
            SaveStage("services-started");
        });

        runStage("Checking every selected service", output =>
        {
            Require(docker.WaitForServices(target, capabilities.ServiceIds, ReadinessTimeout, capabilities.OneShotServiceIds,
                _ => poll?.Invoke(), output), "Some selected services are missing or have not become ready.");
            PersistStorageKeys(context, output);
            SaveStage("services-ready");
        });

        if (capabilities.MonitoringEnabled)
        {
            runStage("Checking metrics and logs", output =>
                Require(docker.WaitForMonitoring(target, ReadinessTimeout, output),
                    "Monitoring endpoints have not become ready. Inspect Prometheus, Loki, Alloy and Grafana logs, then retry with this folder."));
        }

        runStage("Checking the HTTPS address", _ =>
        {
            (httpsVerifier ?? new HttpsEndpointVerifier()).Verify(config, token);
        });

        runStage("Checking the administrator account", output =>
        {
            if (context.IsRepair)
            {
                output("Existing account preserved. Its current password and browser sign-in were not verified; sign in to confirm access.");
                return;
            }

            (adminLoginVerifier ?? new AdminLoginVerifier()).Verify(config, token);
            context.AdminLoginVerified = true;
            output("Fresh administrator login and authenticated profile verified over HTTPS.");
        });

        runStage("Saving the completed installation", _ =>
        {
            InstallationSummary.Write(context);
            SaveStage("complete");
            context.DeploymentVerified = true;
        });

        void SaveStage(string stage)
        {
            token.ThrowIfCancellationRequested();
            config.Stage = stage;
            configuration.SaveConfiguration(config, target);
        }
    }

    public static void PersistStorageKeys(InstallationContext context, Action<string>? output = null)
    {
        context.Cancellation.Token.ThrowIfCancellationRequested();
        var handoff = Path.Combine(context.InstallPath, "init-output", "garage-credentials.env");

        if (File.Exists(handoff))
        {
            GarageCredentialPersistence.Commit(context, handoff);
        }
        else if (!GarageCredentialPersistence.AcknowledgePersistedKeys(context))
        {
            throw new InvalidOperationException("Storage initialization completed without a complete key handoff. Existing files were preserved. " +
                "From this installation's settings folder, rerun the existing initialization job with its saved project name: " +
                "GARAGE_FORCE_CREDENTIAL_EXPORT=true docker compose --project-name PROJECT --file docker-compose.yml --env-file .env up -d --no-deps --force-recreate --no-build --pull never garage-init. " +
                "Replace PROJECT with ProjectName from alexandria-config.json. Wait for garage-init to exit successfully, then rerun setup with the same folder.");
        }

        output?.Invoke("Storage keys are persisted and verified.");
    }

    private static void Require(bool succeeded, string error)
    {
        if (!succeeded) throw new InvalidOperationException(error);
    }
}
