using Alexandria.Data.Models;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.Auth;

/// <summary>
/// Token rotation leans on Postgres row locks: only one refresh may spend a token, and the losers
/// must be able to find what it was replaced by.
/// </summary>
[Collection("Database")]
public class RefreshTokenRotationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _sessionStart = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var db = fixture.CreateDbContext();
        db.Users.Add(new UserBuilder().WithId(_userId).Build());
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await fixture.ResetAsync();
    }

    [Fact]
    public async Task RevokeIfActive_concurrentCallers_exactlyOneWins()
    {
        var token = await SeedAsync(_sessionStart, createdAt: _sessionStart);

        var attempts = Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = fixture.CreateDbContext();
            return await new RefreshTokenRepository(db).RevokeIfActiveAsync(token.Id, DateTime.UtcNow, Ct);
        });

        var results = await Task.WhenAll(attempts);

        results.Count(won => won).Should().Be(1);
    }

    [Fact]
    public async Task GetLiveSuccessor_findsTheSessionsNewestLiveToken()
    {
        var spent = await SeedAsync(_sessionStart, createdAt: _sessionStart.AddHours(1), revoked: true);
        var successor = await SeedAsync(_sessionStart, createdAt: _sessionStart.AddHours(2));
        await SeedAsync(_sessionStart.AddDays(-1), createdAt: _sessionStart.AddHours(3));
        await SeedAsync(_sessionStart, createdAt: _sessionStart.AddHours(4), revoked: true);

        await using var db = fixture.CreateDbContext();
        var found = await new RefreshTokenRepository(db).GetLiveSuccessorAsync(
            _userId, _sessionStart, spent.CreatedAt, DateTime.UtcNow, Ct);

        found.Should().NotBeNull();
        found!.Id.Should().Be(successor.Id);
    }

    [Fact]
    public async Task GetLiveSuccessor_signedOutSession_findsNothing()
    {
        var spent = await SeedAsync(_sessionStart, createdAt: _sessionStart, revoked: true);

        await using var db = fixture.CreateDbContext();
        var found = await new RefreshTokenRepository(db).GetLiveSuccessorAsync(
            _userId, _sessionStart, spent.CreatedAt, DateTime.UtcNow, Ct);

        found.Should().BeNull();
    }

    private async Task<RefreshToken> SeedAsync(DateTime sessionStart, DateTime createdAt, bool revoked = false)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            UserId = _userId,
            SessionStartedAt = sessionStart,
            CreatedAt = createdAt,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = revoked,
            RevokedAt = revoked ? createdAt.AddMinutes(1) : null,
        };

        await using var db = fixture.CreateDbContext();
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(Ct);

        return token;
    }
}
