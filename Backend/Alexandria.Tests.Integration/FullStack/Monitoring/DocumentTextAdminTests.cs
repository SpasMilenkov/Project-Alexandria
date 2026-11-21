using System.Net;
using System.Net.Http.Json;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.DocumentTextStats;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Integration.FullStack.Monitoring;

/// <summary>
/// The admin view of the document text sweep, through the real endpoints and database.
/// </summary>
public class DocumentTextAdminTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string OverviewRoute = "/api/admin/document-text/overview";
    private const string RetryRoute = "/api/admin/document-text/retry-failed";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Overview_and_retry_require_admin()
    {
        var overview = await Auth.GetAsync(OverviewRoute, Ct);
        var retry = await Auth.PostAsync(RetryRoute, null, Ct);

        overview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        retry.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Overview_counts_outcomes_and_names_only_the_admins_own_failures()
    {
        var admin = Factory.CreateAuthenticatedClient(UserId, "Admin");
        var (_, otherId) = await CreateOtherUserAsync();

        var ownFailure = await SeedFileWithVersionAsync(f => f.WithName("my-scan.pdf"));
        var otherFailure = await SeedFileWithVersionAsync(f => f.WithName("their-tax-return.pdf").WithOwner(otherId));
        await RecordFailureAsync(ownFailure, "pdftotext exited with 1");
        await RecordFailureAsync(otherFailure, "Download failed: users/their-tax-return/object");

        var response = await admin.GetAsync(OverviewRoute, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();

        var body = await response.Content.ReadFromJsonAsync<DocumentTextOverviewResponse>(Ct);
        body!.StatusCounts.Should().ContainSingle()
            .Which.Should().Be(new DocumentTextStatusCount(DocumentTextStatus.Failed, 2));
        body.RecentFailures.Should().OnlyContain(f => f.WillRetry);
        body.MaxAttempts.Should().Be(3);

        var own = body.RecentFailures.Single(f => f.FileName == "my-scan.pdf");
        own.ErrorDetail.Should().Be("pdftotext exited with 1");
        own.MimeType.Should().NotBeNull();

        var other = body.RecentFailures.Single(f => f.FileName is null);
        other.ErrorDetail.Should().BeNull();
        other.MimeType.Should().BeNull();

        var json = await response.Content.ReadAsStringAsync(Ct);
        json.Should().NotContain("their-tax-return");
    }

    [Fact]
    public async Task Retry_resets_failed_rows_and_records_one_audit_entry()
    {
        var admin = Factory.CreateAuthenticatedClient(UserId, "Admin");
        var file = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf"));
        await RecordFailureAsync(file, "boom");
        await RecordFailureAsync(file, "boom");

        var response = await admin.PostAsync(RetryRoute, null, Ct);
        var again = await admin.PostAsync(RetryRoute, null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<RetryFailedDocumentTextResponse>(Ct))!.Reset.Should().Be(1);
        // Reset rows stay failed until the sweep tries them, so a second press resets them again.
        (await again.Content.ReadFromJsonAsync<RetryFailedDocumentTextResponse>(Ct))!.Reset.Should().Be(1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var row = await db.DocumentText.AsNoTracking().SingleAsync(Ct);
        row.AttemptCount.Should().Be(0);
        row.LastAttemptAt.Should().BeNull();

        var audits = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EventCode == AuditEventCode.DocumentTextRetried)
            .ToListAsync(Ct);
        audits.Should().HaveCount(2).And.OnlyContain(a => a.UserId == UserId);
    }

    [Fact]
    public async Task Retry_with_nothing_failed_writes_no_audit_entry()
    {
        var admin = Factory.CreateAuthenticatedClient(UserId, "Admin");

        var response = await admin.PostAsync(RetryRoute, null, Ct);

        (await response.Content.ReadFromJsonAsync<RetryFailedDocumentTextResponse>(Ct))!.Reset.Should().Be(0);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        (await db.AuditLogs.AnyAsync(a => a.EventCode == AuditEventCode.DocumentTextRetried, Ct))
            .Should().BeFalse();
    }

    private async Task RecordFailureAsync(File file, string error)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var contentObjectId = await db.FileVersions
            .Where(v => v.Id == file.CurrentVersionId)
            .Select(v => v.ContentObjectId)
            .SingleAsync(Ct);

        // The overview only covers content the sweep can read, which means finished uploads.
        await db.ContentObjects
            .Where(c => c.Id == contentObjectId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPromoted, true), Ct);

        await unitOfWork.DocumentText.RecordFailureAsync(
            contentObjectId, DocumentTextExtractor.CurrentVersion, error, Ct);
    }
}