using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Models;

namespace AuthService;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _config;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _config = config;
    }

    public async Task<AuthResult> AuthenticateAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        
        if (user == null || user.DeletedAt != null)
        {
            return AuthResult.Failed();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        
        if (!result.Succeeded)
        {
            return AuthResult.Failed();
        }

        return AuthResult.Success(user);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var storedToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked);

        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return AuthResult.Failed();
        }

        if (storedToken.User?.DeletedAt != null)
        {
            return AuthResult.Failed();
        }

        // Revoke old token (token rotation)
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return AuthResult.Success(storedToken.User!);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
    {
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (storedToken == null)
        {
            return false;
        }

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.UserName!),
        };

        // Add roles
        var roles = _userManager.GetRolesAsync(user).Result;
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var accessTokenExpiry = _config.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 15);

        var token = JwtBearer.CreateToken(
            signingKey: _config["Jwt:Secret"]!,
            expireAt: DateTime.UtcNow.AddMinutes(accessTokenExpiry),
            claims: claims.ToArray(),
            audience: _config["Jwt:Audience"],
            issuer: _config["Jwt:Issuer"]
        );

        return token;
    }

    public string GenerateRefreshToken(ApplicationUser user)
    {
        var refreshTokenExpiry = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);
        
        // Generate cryptographically secure random token
        var tokenValue = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)
        );

        var refreshToken = new RefreshToken
        {
            Token = tokenValue,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiry),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        _dbContext.SaveChanges();

        return tokenValue;
    }
}
