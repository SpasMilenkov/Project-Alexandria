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
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _otherSessionId = Guid.NewGuid();
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
        var token = await SeedAsync(_sessionId, createdAt: _sessionStart);

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
        var spent = await SeedAsync(_sessionId, createdAt: _sessionStart.AddHours(1), revoked: true);
        var successor = await SeedAsync(_sessionId, createdAt: _sessionStart.AddHours(2));
        await SeedAsync(_otherSessionId, createdAt: _sessionStart.AddHours(3));
        await SeedAsync(_sessionId, createdAt: _sessionStart.AddHours(4), revoked: true);

        await using var db = fixture.CreateDbContext();
        var found = await new RefreshTokenRepository(db).GetLiveSuccessorAsync(
            _userId, _sessionId, spent.CreatedAt, DateTime.UtcNow, Ct);

        found.Should().NotBeNull();
        found!.Id.Should().Be(successor.Id);
    }

    [Fact]
    public async Task GetLiveSuccessor_signedOutSession_findsNothing()
    {
        var spent = await SeedAsync(_sessionId, createdAt: _sessionStart, revoked: true);

        await using var db = fixture.CreateDbContext();
        var found = await new RefreshTokenRepository(db).GetLiveSuccessorAsync(
            _userId, _sessionId, spent.CreatedAt, DateTime.UtcNow, Ct);

        found.Should().BeNull();
    }

    [Fact]
    public async Task RevokeSessionTokens_revokesOnlyThatSessionsLiveTokens()
    {
        await SeedAsync(_sessionId, createdAt: _sessionStart, revoked: true);
        var live = await SeedAsync(_sessionId, createdAt: _sessionStart.AddHours(1));
        var otherSession = await SeedAsync(_otherSessionId, createdAt: _sessionStart.AddHours(2));

        await using (var db = fixture.CreateDbContext())
        {
            var revoked = await new RefreshTokenRepository(db).RevokeSessionTokensAsync(
                _userId, _sessionId, DateTime.UtcNow, Ct);

            revoked.Should().Be(1);
        }

        await using var check = fixture.CreateDbContext();
        (await check.RefreshTokens.FindAsync([live.Id], Ct))!.IsRevoked.Should().BeTrue();
        (await check.RefreshTokens.FindAsync([otherSession.Id], Ct))!.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveBySession_findsTheLiveToken_onlyForItsOwner()
    {
        await SeedAsync(_sessionId, createdAt: _sessionStart, revoked: true);
        var live = await SeedAsync(_sessionId, createdAt: _sessionStart.AddHours(1));

        await using var db = fixture.CreateDbContext();
        var repository = new RefreshTokenRepository(db);

        var own = await repository.GetActiveBySessionAsync(_sessionId, _userId, DateTime.UtcNow, Ct);
        var foreign = await repository.GetActiveBySessionAsync(_sessionId, Guid.NewGuid(), DateTime.UtcNow, Ct);

        own.Should().NotBeNull();
        own!.Id.Should().Be(live.Id);
        foreign.Should().BeNull();
    }

    private async Task<RefreshToken> SeedAsync(Guid sessionId, DateTime createdAt, bool revoked = false)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            UserId = _userId,
            SessionId = sessionId,
            SessionStartedAt = _sessionStart,
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