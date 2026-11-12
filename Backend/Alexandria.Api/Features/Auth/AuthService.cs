using System.Security.Claims;
using System.Security.Cryptography;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Sessions;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;

namespace Alexandria.Api.Features.Auth;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IUnitOfWork unitOfWork,
    IConfiguration config,
    IAuditWriter auditWriter,
    IAuditActorAccessor auditActor)
    : IAuthService
{
    private const int MaxRecordedEmailLength = 256;

    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(30);

    public async Task<AuthResult> AuthenticateAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is not { DeletedAt: null } || user.Id == SystemConfig.SystemId)
        {
            await RecordLoginFailedAsync(null, email, LoginFailureReason.UnknownUser, ct);
            return AuthResult.Failed(LoginFailureReason.UnknownUser);
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            var reason = FailureReasonOf(result);
            await RecordLoginFailedAsync(user, email, reason, ct);
            return AuthResult.Failed(reason);
        }

        await RecordLoginAsync(user, ct);

        var authResult = AuthResult.SetSuccess(user);

        var roles = await userManager.GetRolesAsync(user);

        authResult.UserRoles = roles;
        authResult.OnboardingStep = user.OnboardinStep;

        return authResult;
    }

    public async Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default)
    {
        auditWriter.Record(new AuditEntry
        {
            EventCode = AuditEventCode.UserLogout,
            OperationType = OperationType.Logout,
            EntityType = EntityType.User,
            EntityId = userId,
            OwnerId = userId,
        });

        // Revoking saves the context, which also commits the staged entry.
        if (!string.IsNullOrEmpty(refreshToken) && await RevokeRefreshTokenAsync(refreshToken, ct))
            return;

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserSessionDto>> GetSessionsAsync(Guid userId, string? currentRefreshToken,
        CancellationToken ct = default)
    {
        var tokens = await unitOfWork.RefreshTokens.GetActiveByUserAsync(userId, DateTime.UtcNow, ct);

        return tokens
            .Select(t => new UserSessionDto
            {
                Id = t.Id,
                SessionStartedAt = t.SessionStartedAt,
                LastActiveAt = t.CreatedAt,
                ExpiresAt = t.ExpiresAt,
                IpAddress = t.CreatedByIp,
                UserAgent = t.UserAgent,
                IsCurrent = currentRefreshToken is not null && t.Token == currentRefreshToken,
            })
            .ToList();
    }

    public async Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var token = await unitOfWork.RefreshTokens.GetActiveByIdAsync(sessionId, userId, DateTime.UtcNow, ct);
        if (token is null)
            return false;

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        unitOfWork.RefreshTokens.Update(token);

        auditWriter.Record(new AuditEntry
        {
            EventCode = AuditEventCode.UserSessionRevoked,
            OperationType = OperationType.Update,
            EntityType = EntityType.Session,
            EntityId = token.Id,
            OwnerId = userId,
            Metadata = new Dictionary<string, object?>
            {
                ["session_started_at"] = token.SessionStartedAt,
                ["ip_address"] = token.CreatedByIp,
                ["user_agent"] = token.UserAgent,
            },
        });

        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task RecordLoginAsync(ApplicationUser user, CancellationToken ct)
    {
        var ip = auditActor.IpAddress;
        bool? newIp = null;

        if (ip is not null)
        {
            var seenBefore = await unitOfWork.AuditLogs.AnyAsync(new AuditLogFilter
            {
                OwnerId = user.Id,
                EventCodes = [AuditEventCode.UserLogin],
                IpAddress = ip,
            }, ct);

            newIp = !seenBefore;
        }

        // The request is still anonymous at this point, so the actor is set explicitly.
        auditWriter.Record(new AuditEntry
        {
            EventCode = AuditEventCode.UserLogin,
            OperationType = OperationType.Login,
            EntityType = EntityType.User,
            EntityId = user.Id,
            OwnerId = user.Id,
            EntityName = user.UserName,
            ActorId = user.Id,
            Metadata = new Dictionary<string, object?>
            {
                ["user_agent"] = auditActor.UserAgent,
                ["new_ip"] = newIp,
            },
        });

        await unitOfWork.SaveChangesAsync(ct);
    }

    // Nobody is signed in yet, so the system is the actor; the targeted account is the owner and
    // sees the attempt in its security history.
    private async Task RecordLoginFailedAsync(ApplicationUser? user, string email, LoginFailureReason reason,
        CancellationToken ct)
    {
        var recordedEmail = email.Length > MaxRecordedEmailLength ? email[..MaxRecordedEmailLength] : email;

        auditWriter.Record(new AuditEntry
        {
            EventCode = AuditEventCode.UserLoginFailed,
            OperationType = OperationType.Login,
            EntityType = EntityType.User,
            EntityId = user?.Id,
            OwnerId = user?.Id,
            EntityName = user?.UserName,
            Metadata = new Dictionary<string, object?>
            {
                ["reason"] = ReasonName(reason),
                ["email"] = recordedEmail,
                ["user_agent"] = auditActor.UserAgent,
            },
        });

        await unitOfWork.SaveChangesAsync(ct);
    }

    private static LoginFailureReason FailureReasonOf(SignInResult result)
    {
        if (result.IsLockedOut)
            return LoginFailureReason.LockedOut;

        if (result.IsNotAllowed)
            return LoginFailureReason.NotAllowed;

        return LoginFailureReason.BadPassword;
    }

    private static string ReasonName(LoginFailureReason reason) => reason switch
    {
        LoginFailureReason.UnknownUser => "unknown_user",
        LoginFailureReason.LockedOut => "locked_out",
        LoginFailureReason.NotAllowed => "not_allowed",
        _ => "bad_password",
    };

    public async Task<AuthResult> GetSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is not { DeletedAt: null } || user.Id == SystemConfig.SystemId)
            return AuthResult.Failed();

        var authResult = AuthResult.SetSuccess(user);

        authResult.UserRoles = await userManager.GetRolesAsync(user);
        authResult.OnboardingStep = user.OnboardinStep;

        return authResult;
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var storedToken = await unitOfWork.RefreshTokens.GetByTokenWithUserAsync(refreshToken, ct);

        if (storedToken?.User is not { DeletedAt: null } || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return AuthResult.Failed();
        }

        if (await userManager.IsLockedOutAsync(storedToken.User))
        {
            return AuthResult.Failed();
        }

        if (storedToken.IsRevoked)
            return await ResumeRotatedSessionAsync(storedToken, storedToken.RevokedAt, ct);

        try
        {
            await unitOfWork.BeginTransactionAsync(ct);

            var revokedAt = DateTime.UtcNow;

            if (!await unitOfWork.RefreshTokens.RevokeIfActiveAsync(storedToken.Id, revokedAt, ct))
            {
                // Another request rotated this token while this one was on its way.
                await unitOfWork.RollbackAsync(ct);
                return await ResumeRotatedSessionAsync(storedToken, revokedAt, ct);
            }

            var newRefreshToken = new Data.Models.RefreshToken
            {
                Token = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(64)
                ),
                UserId = storedToken.UserId,
                ExpiresAt = DateTime.UtcNow.AddDays(config.GetValue("Jwt:RefreshTokenExpiryDays", 7)),
                SessionStartedAt = storedToken.SessionStartedAt,
                CreatedByIp = auditActor.IpAddress ?? storedToken.CreatedByIp,
                UserAgent = auditActor.UserAgent ?? storedToken.UserAgent,
            };

            await unitOfWork.RefreshTokens.AddAsync(newRefreshToken, ct);

            await unitOfWork.SaveChangesAsync(ct);
            await unitOfWork.CommitAsync(ct);

            var result = AuthResult.SetSuccess(storedToken.User);
            result.RefreshToken = newRefreshToken.Token;

            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    // Tabs share one refresh cookie, so two of them can spend the same token at once. The loser
    // gets the session's current token instead of a failure, which would clear the cookies and
    // sign every tab out. Past the grace period a spent token is refused as before.
    private async Task<AuthResult> ResumeRotatedSessionAsync(Data.Models.RefreshToken spent, DateTime? revokedAt,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        if (revokedAt is null || now - revokedAt.Value > RotationGrace)
            return AuthResult.Failed();

        // A token revoked by sign-out or session removal has no successor, so it stays refused.
        var successor = await unitOfWork.RefreshTokens.GetLiveSuccessorAsync(
            spent.UserId, spent.SessionStartedAt, spent.CreatedAt, now, ct);

        if (successor is null)
            return AuthResult.Failed();

        var result = AuthResult.SetSuccess(spent.User!);
        result.RefreshToken = successor.Token;

        return result;
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

        var accessTokenExpiry = config.GetValue("Jwt:AccessTokenExpiryMinutes", 15);
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
        var refreshTokenExpiry = config.GetValue("Jwt:RefreshTokenExpiryDays", 7);

        var tokenValue = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64)
        );

        var refreshToken = new Data.Models.RefreshToken
        {
            Token = tokenValue,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiry),
            SessionStartedAt = DateTime.UtcNow,
            CreatedByIp = auditActor.IpAddress,
            UserAgent = auditActor.UserAgent,
        };

        await unitOfWork.RefreshTokens.AddAsync(refreshToken, ct);

        return tokenValue;
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default)
    {
        await unitOfWork.RefreshTokens.RevokeUserTokensAsync(userId, ct);
    }

    public async Task ChangeInitialPasswordAsync(Guid userId, string oldPassword, string newPassword,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ??
                   throw new InvalidOperationException("User does not exist");

        if (user.OnboardinStep != OnboardingStep.SetPassword)
            throw new InvalidOperationException("Initial password already changed");

        var result = await signInManager.CheckPasswordSignInAsync(user, oldPassword, lockoutOnFailure: false);

        if (!result.Succeeded) throw new InvalidOperationException("Initial password does not match");

        await userManager.ChangePasswordAsync(user, oldPassword, newPassword);

        user.OnboardinStep = OnboardingStep.CompleteProfile;
        await userManager.UpdateAsync(user);
    }
}