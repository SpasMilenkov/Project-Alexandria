using Common;
using Common.Services;
using DTO;
using DTO.Audit;
using DTO.Files;

namespace Storage;

public class AuditService(IUnitOfWork unitOfWork) : IAuditService
{
    //Todo: Add a Cron job that runs periodically to clean up old audit logs
    //via delete range
    public async Task Log(AuditLogDto action)
    {
        await unitOfWork.AuditLogs.AddAsync(action);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<PaginatedResult<AuditLogResult>> GetLogs(AuditLogQuery query, Guid userId,
        CancellationToken ct = default)
    {
        if (query.UserId != userId) throw new InvalidOperationException(
            "For logs current user session id should" +
            " match the ID that was used in the query." +
            " In the future this exception won't be" +
            " thrown if the user requesting the logs " +
            "has administration permissions");
        return await unitOfWork.AuditLogs.FetchAuditLogAsync(query, ct);
    }

}