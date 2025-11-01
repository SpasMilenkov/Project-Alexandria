using System.Security.Claims;
using Common;
using DTO;
using Microsoft.AspNetCore.Identity;
using Models;
using FastEndpoints.Security;

namespace API.Features.Auth;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IUnitOfWork unitOfWork,
    IConfiguration config)
    : IAuthService
{
    public async Task<AuthResult> AuthenticateAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        
        if (user is not { DeletedAt: null })
        {
            return AuthResult.Failed();
        }
        
        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        
        if (!result.Succeeded)
        {
            return AuthResult.Failed();
        }
        
        return AuthResult.SetSuccess(user);
    }
    
    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var storedToken = await unitOfWork.RefreshTokens.GetByTokenWithUserAsync(refreshToken, ct);
        
        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow || storedToken.User?.DeletedAt != null)
        {
            return AuthResult.Failed();
        }
        
        try
        {
            await unitOfWork.BeginTransactionAsync(ct);
            
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            unitOfWork.RefreshTokens.Update(storedToken);
            
            var newRefreshToken = new Models.RefreshToken
            {
                Token = Convert.ToBase64String(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)
                ),
                UserId = storedToken.UserId,
                ExpiresAt = DateTime.UtcNow.AddDays(config.GetValue("Jwt:RefreshTokenExpiryDays", 7))
            };
            
            await unitOfWork.RefreshTokens.AddAsync(newRefreshToken, ct);
            
            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);
            
            var result = AuthResult.SetSuccess(storedToken.User!);
            result.RefreshToken = newRefreshToken.Token;
            
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
    
    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        return await unitOfWork.RefreshTokens.RevokeTokenAsync(refreshToken, ct);
    }
    
    public string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.UserName!),
        };
        
        var roles = userManager.GetRolesAsync(user).Result;
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        
        var accessTokenExpiry = config.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 15);
        var token = JwtBearer.CreateToken(o =>
        {
            o.SigningKey = config["Jwt:Secret"]!;
            o.ExpireAt = DateTime.UtcNow.AddMinutes(accessTokenExpiry);
            o.User.Claims.AddRange(claims.ToArray());
            o.Audience = config["Jwt:Audience"];
            o.Issuer = config["Jwt:Issuer"];
        });
        
        return token;
    }
    
    public async Task<string> GenerateRefreshTokenAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var refreshTokenExpiry = config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);
        
        var tokenValue = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)
        );
        
        var refreshToken = new Models.RefreshToken
        {
            Token = tokenValue,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiry)
        };
        
        await unitOfWork.RefreshTokens.AddAsync(refreshToken, ct);
        
        return tokenValue;
    }
    
    public async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.RefreshTokens.RevokeUserTokensAsync(userId, ct);
    }
}