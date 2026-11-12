using System.Text;
using System.Text.Json;
using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;

namespace Alexandria.Tests.Unit.Audit;

public class AuditExportWriterTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("plain.txt", "plain.txt")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("@sum", "'@sum")]
    public void EscapeCsv_quotesAndNeutralisesFormulas(string? value, string expected)
    {
        AuditExportWriter.EscapeCsv(value).Should().Be(expected);
    }

    [Fact]
    public async Task WriteAsync_json_streamsAValidArrayWithoutBlockingWrites()
    {
        var body = new AsyncOnlyStream();
        var context = new DefaultHttpContext { Response = { Body = body } };

        await AuditExportWriter.WriteAsync(context.Response, Rows(Row(), Row()), AuditExportFormat.Json,
            "activity", TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(body.ToArray());
        json.RootElement.GetArrayLength().Should().Be(2);
        json.RootElement[0].GetProperty("eventCode").GetInt32().Should().Be((int)AuditEventCode.FileDownloaded);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task WriteAsync_csv_writesHeaderAndRowsWithoutBlockingWrites()
    {
        var body = new AsyncOnlyStream();
        var context = new DefaultHttpContext { Response = { Body = body } };

        await AuditExportWriter.WriteAsync(context.Response, Rows(Row()), AuditExportFormat.Csv, "activity",
            TestContext.Current.CancellationToken);

        var lines = Encoding.UTF8.GetString(body.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("timestamp,event_code");
    }

    private static AuditLogResult Row() => new()
    {
        Id = Guid.NewGuid(),
        OperationType = OperationType.Read,
        EntityType = EntityType.File,
        UserId = Guid.NewGuid(),
        EntityId = Guid.NewGuid(),
        EntityName = "song.flac",
        EventCode = AuditEventCode.FileDownloaded,
        Metadata = """{"version_number":2}""",
        Timestamp = DateTimeOffset.UtcNow,
        LogSource = LogSource.API,
    };

    private static async IAsyncEnumerable<AuditLogResult> Rows(params AuditLogResult[] rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return row;
        }
    }
}
