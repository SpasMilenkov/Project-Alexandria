using System.Data.Common;
using System.Security.Claims;
using Alexandria.Common.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Alexandria.Data.Interceptors;

public class AuditInterceptor(
    IHttpContextAccessor httpContextAccessor,
    AuditContext auditContext)
    : DbTransactionInterceptor
{
    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        await SetSessionContextAsync(connection, cancellationToken);
        return await base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
    }

    private async Task SetSessionContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        string? userId;
        var ipAddress = "";

        if (auditContext.UserId.HasValue)
        {
            userId = auditContext.UserId.Value.ToString();
        }
        else
        {
            var httpContext = httpContextAccessor.HttpContext;

            userId = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? httpContext?.User?.FindFirst("sub")?.Value;

            var forwarded = httpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            ipAddress = forwarded?.Split(',').FirstOrDefault()?.Trim()
                        ?? httpContext?.Request.Headers["X-Real-IP"].FirstOrDefault()
                        ?? httpContext?.Connection?.RemoteIpAddress?.ToString()
                        ?? "";
        }

        if (userId is null) return;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT set_config('app.current_user_id', @userId, true);
            SELECT set_config('app.current_ip', @ipAddress, true);
        ";

        var userIdParam = cmd.CreateParameter();
        userIdParam.ParameterName = "@userId";
        userIdParam.Value = userId;
        cmd.Parameters.Add(userIdParam);

        var ipParam = cmd.CreateParameter();
        ipParam.ParameterName = "@ipAddress";
        ipParam.Value = ipAddress;
        cmd.Parameters.Add(ipParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}