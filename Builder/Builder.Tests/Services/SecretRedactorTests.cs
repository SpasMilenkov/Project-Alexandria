using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Services;

public class SecretRedactorTests
{
    [Fact]
    public void Redact_knownLiteralAndEncodedSecret_keepsUsefulErrorWithoutCredential()
    {
        const string password = "secret $/#=café";
        var sut = new SecretRedactor(() => new Dictionary<string, string> { ["DB_PASSWORD"] = password });

        var result = sut.Redact($"Database refused {password}; AMQP token {Uri.EscapeDataString(password)}; exit 1");

        result.Should().Contain("Database refused").And.Contain("exit 1");
        result.Should().NotContain(password).And.NotContain(Uri.EscapeDataString(password));
    }

    [Theory]
    [InlineData("Host=postgres;Password=unknown-secret;Database=alexandria", "unknown-secret")]
    [InlineData("Host=postgres;Password=\"unknown;\"\"quoted-secret\";Database=alexandria", "quoted-secret")]
    [InlineData("Password='unknown;''quoted-secret';Database=alexandria", "quoted-secret")]
    [InlineData("Connection failed amqp://user:unknown%24secret@rabbitmq:5672/vhost", "unknown%24secret")]
    [InlineData("GARAGE_S3_SECRET_KEY=unknown-secret", "unknown-secret")]
    [InlineData("Secret key: unknown-secret", "unknown-secret")]
    public void Redact_structuredSecretWithoutKnownValue_removesCredential(string input, string secret)
    {
        var sut = new SecretRedactor(() => new Dictionary<string, string>());

        var result = sut.Redact(input);

        result.Should().NotContain(secret).And.Contain("[redacted]");
    }

    [Fact]
    public void Redact_storageKeysCommittedLater_usesCurrentEffectiveValues()
    {
        IReadOnlyDictionary<string, string> effective = new Dictionary<string, string>();
        var sut = new SecretRedactor(() => effective);
        effective = new Dictionary<string, string> { ["GARAGE_S3_SECRET_KEY"] = "newly-persisted-storage-secret" };

        var result = sut.Redact("Storage refused newly-persisted-storage-secret");

        result.Should().Be("Storage refused [redacted]");
    }
}
