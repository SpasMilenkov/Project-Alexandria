using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Auth;

public class GetSessionTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    [Fact]
    public async Task GetSession_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await Anon.GetAsync("/api/auth/session", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSession_Authenticated_ReturnsCurrentUserWithoutCaching()
    {
        var response = await Auth.GetAsync("/api/auth/session", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Cache-Control", out var cacheControl).Should().BeTrue();
        string.Join(",", cacheControl!).Should().Contain("no-store");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: TestContext.Current.CancellationToken);

        body.GetProperty("user").GetProperty("id").GetGuid().Should().Be(UserId);
        body.GetProperty("onboardingStep").GetInt32().Should().Be((int)OnboardingStep.Done);
    }

    [Fact]
    public async Task GetSession_DeletedUser_ReturnsUnauthorized()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var user = await db.Users.SingleAsync(
            candidate => candidate.Id == UserId,
            TestContext.Current.CancellationToken);

        user.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await Auth.GetAsync("/api/auth/session", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
