using System.Data.Common;
using Alexandria.Common.Audit;

namespace Alexandria.Data.Audit;

/// <summary>
/// Writes the actor, IP and correlation id into Postgres settings that the audit triggers read.
/// </summary>
internal static class AuditSessionContext
{
    private const string Sql =
        "SELECT set_config('app.current_user_id', @userId, @isLocal), " +
        "set_config('app.current_ip', @ipAddress, @isLocal), " +
        "set_config('app.correlation_id', @correlationId, @isLocal);";

    public static async Task ApplyAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IAuditActorAccessor actor,
        bool isLocal,
        CancellationToken ct)
    {
        await using var command = CreateCommand(connection, transaction, actor, isLocal);
        await command.ExecuteNonQueryAsync(ct);
    }

    public static void Apply(
        DbConnection connection,
        DbTransaction? transaction,
        IAuditActorAccessor actor,
        bool isLocal)
    {
        using var command = CreateCommand(connection, transaction, actor, isLocal);
        command.ExecuteNonQuery();
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        DbTransaction? transaction,
        IAuditActorAccessor actor,
        bool isLocal)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql;

        // An empty user id makes the triggers fall back to the system actor.
        AddParameter(command, "@userId", actor.UserId?.ToString() ?? "");
        AddParameter(command, "@ipAddress", actor.IpAddress ?? "");
        AddParameter(command, "@correlationId", actor.CorrelationId.ToString());
        AddParameter(command, "@isLocal", isLocal);

        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
