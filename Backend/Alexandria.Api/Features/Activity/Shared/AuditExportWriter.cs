using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Alexandria.Dto.Audit;

namespace Alexandria.Api.Features.Activity.Shared;

/// <summary>
/// Streams rows straight to the response so an export never sits in memory as a whole.
/// </summary>
public static class AuditExportWriter
{
    // Same shape the API returns: camelCase names, enums as numbers.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] CsvHeader =
    [
        "timestamp", "event_code", "operation_type", "entity_type", "entity_id", "entity_name",
        "user_id", "user_name", "owner_id", "parent_entity_id", "correlation_id", "source",
        "ip_address", "metadata"
    ];

    public static async Task WriteAsync(
        HttpResponse response,
        IAsyncEnumerable<AuditLogResult> rows,
        AuditExportFormat format,
        string fileBaseName,
        CancellationToken ct)
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        if (format == AuditExportFormat.Json)
        {
            response.ContentType = "application/json";
            response.Headers.ContentDisposition = $"attachment; filename=\"{fileBaseName}-{stamp}.json\"";
            await WriteJsonAsync(response.BodyWriter, rows, ct);
            return;
        }

        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = $"attachment; filename=\"{fileBaseName}-{stamp}.csv\"";
        await WriteCsvAsync(response.Body, rows, ct);
    }

    // JsonSerializer.Serialize flushes the writer synchronously after every value. Over the
    // response stream that is a blocking write, which Kestrel refuses mid-response, so the JSON
    // goes into the response pipe's buffer and only the pipe flush touches the network.
    private static async Task WriteJsonAsync(PipeWriter body, IAsyncEnumerable<AuditLogResult> rows,
        CancellationToken ct)
    {
        await using var writer = new Utf8JsonWriter(body);
        long flushedAt = 0;

        writer.WriteStartArray();

        await foreach (var row in rows.WithCancellation(ct))
        {
            JsonSerializer.Serialize(writer, row, JsonOptions);

            if (writer.BytesCommitted - flushedAt > 16 * 1024)
            {
                await body.FlushAsync(ct);
                flushedAt = writer.BytesCommitted;
            }
        }

        writer.WriteEndArray();
        writer.Flush();
        await body.FlushAsync(ct);
    }

    private static async Task WriteCsvAsync(Stream body, IAsyncEnumerable<AuditLogResult> rows,
        CancellationToken ct)
    {
        await using var writer = new StreamWriter(body, new UTF8Encoding(false), bufferSize: 16 * 1024,
            leaveOpen: true);

        await writer.WriteLineAsync(string.Join(',', CsvHeader));

        await foreach (var row in rows.WithCancellation(ct))
        {
            string?[] fields =
            [
                row.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                row.EventCode.ToString(),
                row.OperationType.ToString(),
                row.EntityType.ToString(),
                row.EntityId.ToString(),
                row.EntityName,
                row.UserId.ToString(),
                row.UserName,
                row.OwnerId?.ToString(),
                row.ParentEntityId?.ToString(),
                row.CorrelationId?.ToString(),
                row.LogSource.ToString(),
                row.IpAddress,
                row.Metadata,
            ];

            await writer.WriteLineAsync(string.Join(',', fields.Select(EscapeCsv)));
        }

        await writer.FlushAsync(ct);
    }

    // File and folder names are user-controlled, and spreadsheets execute cells that start with
    // a formula character, so those get a leading quote.
    internal static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var safe = value;
        if (safe[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            safe = "'" + safe;

        var needsQuotes = safe.IndexOfAny([',', '"', '\n', '\r']) >= 0;
        if (!needsQuotes)
            return safe;

        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
