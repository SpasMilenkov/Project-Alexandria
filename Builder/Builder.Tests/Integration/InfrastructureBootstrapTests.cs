using Builder.Services;

namespace Builder.Tests.Integration;

public sealed class InfrastructureBootstrapTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public void Bootstrap_freshInfrastructure_commitsRealKeysAndPreservesThemAcrossRepeatAndResume()
    {
        using var fixture = new CachedInfrastructureFixture();
        fixture.Prepare();
        CachedInfrastructureFixture.Require(!GarageCredentialPersistence.HasPersistedKeys(fixture.Context),
            "Fresh infrastructure must start with pending storage keys.");

        var statuses = fixture.Start();

        foreach (var service in CachedInfrastructureFixture.OneShotServices)
        {
            CachedInfrastructureFixture.Require(statuses.Any(status => status.Service == service && status.State == "exited" && status.ExitCode == 0),
                "Both infrastructure initialization jobs must exit successfully.");
        }

        fixture.VerifyDatabaseAndBroker();
        var established = fixture.ReadHandoff();
        fixture.VerifyStorageCredentials();
        fixture.VerifyInitLogsContainNoKeys(established);
        fixture.CommitHandoff();
        var persisted = EnvGenerator.Parse(File.ReadAllText(Path.Combine(fixture.Target, ".env")));
        CachedInfrastructureFixture.Require(GarageCredentialsFileParser.ToEnvValues(established)
                .All(pair => persisted.GetValueOrDefault(pair.Key) == pair.Value),
            "The generated storage credentials did not survive the real write/read path.");

        fixture.RepeatInitialization();
        CachedInfrastructureFixture.Require(!File.Exists(fixture.HandoffPath),
            "Normal repeated initialization must not recreate consumed storage credentials.");
        fixture.RepeatInitialization(forceCredentialExport: true);
        var repeated = fixture.ReadHandoff();
        CachedInfrastructureFixture.Require(established == repeated, "Explicit credential recovery changed established Garage credentials.");
        fixture.VerifyInitLogsContainNoKeys(repeated);
        fixture.CommitHandoff();

        var containers = fixture.CoreContainerIds();
        var resumed = fixture.Reload();

        try
        {
            InstallationOrchestrator.PersistStorageKeys(resumed);
            CachedInfrastructureFixture.Require(GarageCredentialPersistence.HasPersistedKeys(resumed),
                "Repair must recover complete storage keys after the completed handoff is removed.");
            CachedInfrastructureFixture.Require(GarageCredentialsFileParser.ToEnvValues(established)
                    .All(pair => resumed.Config.EnvironmentValues.GetValueOrDefault(pair.Key) == pair.Value),
                "Repair changed the persisted Garage credentials.");
            fixture.WaitForReady();
            CachedInfrastructureFixture.Require(!File.Exists(fixture.HandoffPath), "Reading persisted storage readiness must not recreate the completed handoff.");
            CachedInfrastructureFixture.Require(fixture.CoreContainerIds() == containers, "Repair inspection replaced existing infrastructure containers.");
        }
        finally
        {
            resumed.Cancellation.Dispose();
        }
    }
}
