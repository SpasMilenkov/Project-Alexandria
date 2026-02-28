using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Test.Common.Fixtures;

public class AlexandriaWebFactory : WebApplicationFactory<Program>
{
    private readonly AlexandriaFixture _fixture;

    public AlexandriaWebFactory(AlexandriaFixture fixture)
    {
        _fixture = fixture;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(_fixture.GetConfigurationOverrides());
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, string role = "User")
    {
        var client = CreateClient();
        var token = GenerateJwt(userId, role);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string GenerateJwt(Guid userId, string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(AlexandriaFixture.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer: AlexandriaFixture.TestJwtIssuer,
            audience: AlexandriaFixture.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}