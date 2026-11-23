using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Links;
using Alexandria.Services.Storage.Links;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.Notes;

public class NoteLinkIndexServiceTests
{
    private const string Target = "0b8c7a4e-36f1-4a6c-9a7e-1f0f3c2d4b5a";
    private static readonly string Markdown = $"About <alexandria://file/{Target}>";

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly INoteLinkRepository _noteLinks = Substitute.For<INoteLinkRepository>();
    private readonly IContentStorageService _contentStorage = Substitute.For<IContentStorageService>();

    private readonly FileVersion _version;
    private readonly NoteLinkScanCandidate _candidate;
    private readonly List<NoteLinkScan> _stored = [];

    public NoteLinkIndexServiceTests()
    {
        _unitOfWork.FileVersions.Returns(_versions);
        _unitOfWork.NoteLinks.Returns(_noteLinks);

        _version = new FileVersion
        {
            Id = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            ContentObjectId = Guid.NewGuid(),
            Size = 10,
            VersionNumber = 1,
            MimeType = "text/markdown",
        };

        _candidate = new NoteLinkScanCandidate
        {
            ContentObjectId = _version.ContentObjectId,
            VersionId = _version.Id,
            OwnerId = Guid.NewGuid(),
        };

        _versions.GetByIdAsync(_version.Id, Arg.Any<CancellationToken>()).Returns(_version);
        _noteLinks.GetScannerVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((int?)null);
        _noteLinks.ReplaceAsync(Arg.Do<NoteLinkScan>(_stored.Add), Arg.Any<CancellationToken>()).Returns(true);
    }

    private NoteLinkIndexService CreateSut() =>
        new(_unitOfWork, _contentStorage, Substitute.For<ILogger<NoteLinkIndexService>>());

    [Fact]
    public async Task IndexVersion_newText_storesItsLinksUnderTheContent()
    {
        await CreateSut().IndexVersionAsync(_version.Id, Markdown, TestContext.Current.CancellationToken);

        var scan = _stored.Should().ContainSingle().Subject;
        scan.ContentObjectId.Should().Be(_version.ContentObjectId);
        scan.ScannerVersion.Should().Be(NoteLinkExtractor.CurrentVersion);

        var link = scan.Links.Should().ContainSingle().Subject;
        link.TargetKind.Should().Be(LinkTargetKind.File);
        link.TargetId.Should().Be(Guid.Parse(Target));
        link.ContentObjectId.Should().Be(_version.ContentObjectId);
    }

    [Fact]
    public async Task IndexVersion_contentAlreadyRead_writesNothing()
    {
        _noteLinks.GetScannerVersionAsync(_version.ContentObjectId, Arg.Any<CancellationToken>())
            .Returns(NoteLinkExtractor.CurrentVersion);

        await CreateSut().IndexVersionAsync(_version.Id, Markdown, TestContext.Current.CancellationToken);

        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task IndexVersion_readByAnOlderExtractor_readsItAgain()
    {
        _noteLinks.GetScannerVersionAsync(_version.ContentObjectId, Arg.Any<CancellationToken>())
            .Returns(NoteLinkExtractor.CurrentVersion - 1);

        await CreateSut().IndexVersionAsync(_version.Id, Markdown, TestContext.Current.CancellationToken);

        _stored.Should().ContainSingle();
    }

    [Fact]
    public async Task IndexVersion_encryptedVersion_isNeverIndexed()
    {
        _version.IsEncrypted = true;

        await CreateSut().IndexVersionAsync(_version.Id, Markdown, TestContext.Current.CancellationToken);

        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task IndexVersion_storeFails_doesNotThrow()
    {
        _noteLinks.ReplaceAsync(Arg.Any<NoteLinkScan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database down"));

        var act = () => CreateSut().IndexVersionAsync(_version.Id, Markdown, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IndexCandidate_readsTheVersionAsItsOwnerAndStoresItsLinks()
    {
        _contentStorage.ReadVersionTextAsync(_candidate.VersionId, _candidate.OwnerId, Arg.Any<CancellationToken>())
            .Returns(new VersionText(_version.FileId, _version.Id, "text/markdown",
                [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Markdown)]));

        await CreateSut().IndexCandidateAsync(_candidate, TestContext.Current.CancellationToken);

        _stored.Should().ContainSingle().Subject.Links.Should().ContainSingle()
            .Which.Snippet.Should().Be(Markdown);
    }

    [Fact]
    public async Task IndexCandidate_alreadyReadByTheEditor_skipsTheStorageRead()
    {
        _noteLinks.GetScannerVersionAsync(_candidate.ContentObjectId, Arg.Any<CancellationToken>())
            .Returns(NoteLinkExtractor.CurrentVersion);

        await CreateSut().IndexCandidateAsync(_candidate, TestContext.Current.CancellationToken);

        await _contentStorage.DidNotReceive().ReadVersionTextAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexCandidate_versionGoneMeanwhile_writesNothing()
    {
        _contentStorage.ReadVersionTextAsync(_candidate.VersionId, _candidate.OwnerId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileVersionNotFoundException(_candidate.VersionId));

        await CreateSut().IndexCandidateAsync(_candidate, TestContext.Current.CancellationToken);

        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task IndexCandidate_bytesMissingForGood_recordsAnEmptyScan()
    {
        _contentStorage.ReadVersionTextAsync(_candidate.VersionId, _candidate.OwnerId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ContentObjectNotFoundException(_candidate.ContentObjectId));

        await CreateSut().IndexCandidateAsync(_candidate, TestContext.Current.CancellationToken);

        _stored.Should().ContainSingle().Subject.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task IndexCandidate_storageDown_throwsForTheSweepToHandle()
    {
        _contentStorage.ReadVersionTextAsync(_candidate.VersionId, _candidate.OwnerId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("storage unreachable"));

        var act = () => CreateSut().IndexCandidateAsync(_candidate, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
        _stored.Should().BeEmpty();
    }
}
