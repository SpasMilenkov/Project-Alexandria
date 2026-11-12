using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Users;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Access;

public sealed partial class AccessOverviewService(IUnitOfWork unitOfWork, ILogger<AccessOverviewService> logger)
    : IAccessOverviewService
{
    private const int TopListSize = 5;
    private const int NetworkListSize = 20;

    private static readonly Dictionary<string, LoginFailureReason> ReasonNames = new()
    {
        ["unknown_user"] = LoginFailureReason.UnknownUser,
        ["bad_password"] = LoginFailureReason.BadPassword,
        ["locked_out"] = LoginFailureReason.LockedOut,
        ["not_allowed"] = LoginFailureReason.NotAllowed,
    };

    public async Task<AccessOverview> GetOverviewAsync(int windowDays, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var accounts = await unitOfWork.Users.GetAccountStatesAsync(ct);
        var sessions = await unitOfWork.RefreshTokens.GetAllActiveAsync(now.UtcDateTime, ct);
        var signIns = await unitOfWork.AuditLogs.GetSignInRowsAsync(AuditEventCode.UserLogin,
            now.AddDays(-windowDays), false, ct);
        var history = await unitOfWork.AuditLogs.GetSignInHistoryAsync(ct: ct);
        var failedRows = await unitOfWork.AuditLogs.GetSignInRowsAsync(AuditEventCode.UserLoginFailed,
            now.AddDays(-2 * windowDays), true, ct);

        var input = new AccessOverviewInput
        {
            Accounts = accounts,
            Sessions = sessions,
            SignIns = signIns,
            History = history,
            Failures = failedRows.Select(ReadFailure).ToList(),
        };

        return Build(input, now, windowDays);
    }

    private FailedSignIn ReadFailure(AuditSignInRow row)
    {
        string? reason = null;
        string? email = null;

        if (!string.IsNullOrEmpty(row.MetadataJson))
        {
            try
            {
                using var document = JsonDocument.Parse(row.MetadataJson);
                reason = ReadString(document.RootElement, "reason");
                email = ReadString(document.RootElement, "email");
            }
            catch (JsonException ex)
            {
                LogUnreadableFailure(logger, row.Timestamp, ex);
            }
        }

        var parsedReason = reason is not null && ReasonNames.TryGetValue(reason, out var known)
            ? known
            : (LoginFailureReason?)null;

        return new FailedSignIn(row.AccountId, row.IpAddress, parsedReason, email, row.Timestamp);
    }

    private static string? ReadString(JsonElement root, string key)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty(key, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;

        return value.GetString();
    }

    internal static AccessOverview Build(AccessOverviewInput input, DateTimeOffset now, int windowDays)
    {
        var windowStart = now.AddDays(-windowDays);
        var previousStart = now.AddDays(-2 * windowDays);
        var liveIds = input.Accounts.Select(a => a.Id).ToHashSet();

        var failedInWindow = input.Failures.Where(f => f.Timestamp >= windowStart).ToList();
        var failedPrevious = input.Failures.Count(f => f.Timestamp >= previousStart && f.Timestamp < windowStart);

        var liveSessions = input.Sessions.Where(s => liveIds.Contains(s.UserId)).ToList();
        var signInsByAccount = input.SignIns.ToLookup(r => r.AccountId);
        var historyByAccount = input.History.ToLookup(h => h.AccountId);

        var accounts = input.Accounts
            .Select(account => SummarizeAccount(account, signInsByAccount[account.Id], historyByAccount[account.Id],
                failedInWindow, liveSessions, windowStart))
            .OrderByDescending(a => a.LastSignInAt.HasValue)
            .ThenByDescending(a => a.LastSignInAt)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AccessOverview
        {
            WindowDays = windowDays,
            ActiveAccounts = accounts.Count(a => a.SignIns > 0),
            TotalAccounts = accounts.Count,
            LiveSessions = liveSessions.Count,
            LockedAccounts = input.Accounts.Count(a => a.LockoutEnd > now),
            FailedSignIns = failedInWindow.Count,
            FailedSignInsPrevious = failedPrevious,
            Accounts = accounts,
            FailedByReason = CountReasons(failedInWindow),
            FailedSources = RankSources(failedInWindow),
            UnknownAccountAttempts = RankUnknownAccounts(failedInWindow),
            Networks = SummarizeNetworks(input.History, input.SignIns),
        };
    }

    private static AccountAccessSummary SummarizeAccount(AccountStateRow account,
        IEnumerable<AuditSignInRow> signInsInWindow, IEnumerable<AuditSignInHistoryRow> history,
        IReadOnlyList<FailedSignIn> failedInWindow, IReadOnlyList<AccountSessionRow> liveSessions,
        DateTimeOffset windowStart)
    {
        var addresses = history.ToList();
        var sessions = liveSessions.Where(s => s.UserId == account.Id).ToList();

        // An address counts as new when the account's first sign-in from it falls inside the window.
        var newNetworks = addresses.Count(h => !string.IsNullOrEmpty(h.IpAddress) && h.FirstSeen >= windowStart);

        DateTimeOffset? lastSignIn = addresses.Count == 0 ? null : addresses.Max(h => h.LastSeen);

        return new AccountAccessSummary
        {
            UserId = account.Id,
            Name = account.Name,
            Email = account.Email,
            LastSignInAt = lastSignIn,
            SignIns = signInsInWindow.Count(),
            NewNetworks = newNetworks,
            FailedSignIns = failedInWindow.Count(f => f.AccountId == account.Id),
            LiveSessions = sessions.Count,
            SessionUserAgents = sessions
                .Select(s => s.UserAgent)
                .OfType<string>()
                .Where(agent => agent.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            LockoutEnd = account.LockoutEnd,
        };
    }

    private static List<FailedSignInReasonCount> CountReasons(IReadOnlyList<FailedSignIn> failures) =>
        failures
            .Where(f => f.Reason.HasValue)
            .GroupBy(f => f.Reason!.Value)
            .Select(g => new FailedSignInReasonCount { Reason = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.Reason)
            .ToList();

    private static List<FailedSignInSource> RankSources(IReadOnlyList<FailedSignIn> failures) =>
        failures
            .Where(f => !string.IsNullOrEmpty(f.IpAddress))
            .GroupBy(f => f.IpAddress!)
            .Select(g => new FailedSignInSource
            {
                IpAddress = g.Key,
                Count = g.Count(),
                AccountCount = g.Where(f => f.AccountId.HasValue).Select(f => f.AccountId).Distinct().Count(),
                LastSeen = g.Max(f => f.Timestamp),
            })
            .OrderByDescending(s => s.Count)
            .ThenByDescending(s => s.LastSeen)
            .Take(TopListSize)
            .ToList();

    private static List<UnknownAccountAttempt> RankUnknownAccounts(IReadOnlyList<FailedSignIn> failures) =>
        failures
            .Where(f => f.AccountId is null && !string.IsNullOrWhiteSpace(f.Email))
            .GroupBy(f => f.Email!.Trim().ToLowerInvariant())
            .Select(g => new UnknownAccountAttempt
            {
                Email = g.Key,
                Count = g.Count(),
                LastSeen = g.Max(f => f.Timestamp),
            })
            .OrderByDescending(a => a.Count)
            .ThenByDescending(a => a.LastSeen)
            .Take(TopListSize)
            .ToList();

    private static List<AccessNetwork> SummarizeNetworks(IReadOnlyList<AuditSignInHistoryRow> history,
        IReadOnlyList<AuditSignInRow> signInsInWindow)
    {
        var firstSeen = history
            .Where(h => !string.IsNullOrEmpty(h.IpAddress))
            .GroupBy(h => h.IpAddress!)
            .ToDictionary(g => g.Key, g => g.Min(h => h.FirstSeen));

        return signInsInWindow
            .Where(r => !string.IsNullOrEmpty(r.IpAddress))
            .GroupBy(r => r.IpAddress!)
            .Select(g => new AccessNetwork
            {
                IpAddress = g.Key,
                AccountCount = g.Where(r => r.AccountId.HasValue).Select(r => r.AccountId).Distinct().Count(),
                SignIns = g.Count(),
                FirstSeen = firstSeen[g.Key],
                LastSeen = g.Max(r => r.Timestamp),
            })
            .OrderByDescending(n => n.LastSeen)
            .Take(NetworkListSize)
            .ToList();
    }
}
