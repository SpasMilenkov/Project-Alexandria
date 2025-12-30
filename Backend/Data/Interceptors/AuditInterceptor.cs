using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Data.Interceptors;

// The interceptor sets variables before EVERY SaveChanges
public class AuditInterceptor(IHttpContextAccessor httpContextAccessor) 
    : DbTransactionInterceptor
{
    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        await SetTransactionContextAsync(connection);
        return await base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
    }
    
    private async Task SetTransactionContextAsync(DbConnection connection)
    {
        var httpContext = httpContextAccessor.HttpContext;
    
        var userId = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? httpContext?.User?.FindFirst("sub")?.Value
                     ?? Guid.Empty.ToString();
    
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "";
    
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
    
        await cmd.ExecuteNonQueryAsync();
    }
}