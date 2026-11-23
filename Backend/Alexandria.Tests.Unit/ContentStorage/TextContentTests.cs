using System.Linq.Expressions;
using System.Text;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Notes;
using Alexandria.Common.Queues;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Content;
using Alexandria.Services.Storage.Content.Mime;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Blake3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.ContentStorage;

public class TextContentTests
{
    private const string Markdown = "text/markdown";

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IContentObjectRepository _contentObjects = Substitute.For<IContentObjectRepository>();
    private readonly IUploadRepository _uploads = Substitute.For<IUploadRepository>();
    private readonly IFileDraftRepository _drafts = Substitute.For<IFileDraftRepository>();
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IPromotionQueue _promotionQueue = Substitute.For<IPromotionQueue>();
    private readonly IFileService _fileService = Substitute.For<IFileService>();
    private readonly S3Config _config = new();
    private readonly Guid _userId = Guid.NewGuid();

    private readonly File _file;
    private readonly FileVersion _parent;
    // Stored only in the tests that make the drafts repository return it.
    private readonly FileDraft _draft;
    private readonly List<FileVersion> _addedVersions = [];
    private Upload? _staged;

    public TextContentTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.FileVersions.Returns(_versions);
        _unitOfWork.ContentObjects.Returns(_contentObjects);
        _unitOfWork.Uploads.Returns(_uploads);
        _unitOfWork.FileDrafts.Returns(_drafts);
        _drafts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FileDraft?)null);
        _drafts.GetRevisionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _parent = new FileVersion
        {
            Id = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            ContentHash = Digest("old text"),
            Size = 8,
            VersionNumber = 3,
            MimeType = Markdown,
        };

        // Renamed since it was uploaded; the save must keep the markdown type all the same.
        _file = new File
        {
            Id = _parent.FileId,
            Name = "notes.txt",
            MimeType = Markdown,
            OwnerId = _userId,
            DirectoryId = Guid.NewGuid(),
            CurrentVersionId = _parent.Id,
        };

        _draft = new FileDraft
        {
            FileId = _file.Id,
            BaseVersionId = _parent.Id,
            Revision = Guid.NewGuid(),
            Content = "draft text",
        };

        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_file);
        _files.GetForUpdateAsync(_file.Id, _userId, Arg.Any<CancellationToken>()).Returns(_file);
        _versions.FirstOrDefaultAsync(Arg.Any<Expression<Func<FileVersion, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_parent);
        _versions.GetByIdAsync(_parent.Id, Arg.Any<CancellationToken>()).Returns(_parent);
        _versions.AddAsync(Arg.Do<FileVersion>(_addedVersions.Add), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var version = call.Arg<FileVersion>();
                version.Id = Guid.NewGuid();
                return version;
            });

        _uploads.AddAsync(Arg.Do<Upload>(upload => _staged = upload), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Upload>());
        _uploads.FirstOrDefaultAsync(Arg.Any<Expression<Func<Upload, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_ => _staged);

        _contentObjects.HashExistsAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((ContentObject?)null);

        _s3.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        _fileService.FolderWithOwnershipExistsAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private ContentStorageService CreateSut() => new(
        _s3,
        Substitute.For<IAmazonS3>(),
        _unitOfWork,
        Options.Create(_config),
        Substitute.For<ILogger<ContentStorageService>>(),
        _promotionQueue,
        _fileService,
        new AuditContext(),
        Substitute.For<IUserSettingsService>(),
        Substitute.For<IPublisherService>(),
        new MimeTypeResolver(Substitute.For<IMimeSniffer>()));

    private static byte[] Digest(string text) => Hasher.Hash(Encoding.UTF8.GetBytes(text)).AsSpan().ToArray();


    [Fact]
    public async Task SaveText_newText_addsTheNextVersionKeepingTheParentsType()
    {
        var saved = await CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, "# New text", _userId,
            TestContext.Current.CancellationToken);

        saved.Unchanged.Should().BeFalse();
        saved.VersionNumber.Should().Be(4);
        saved.MimeType.Should().Be(Markdown);

        var version = _addedVersions.Should().ContainSingle().Subject;
        version.MimeType.Should().Be(Markdown);
        version.VersionNumber.Should().Be(4);
        version.ContentHash.Should().Equal(Digest("# New text"));
        _file.CurrentVersionId.Should().Be(version.Id);

        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.BucketName == _config.TempBucket && r.Key == $"content/{_staged!.TempObjectKey}"),
            Arg.Any<CancellationToken>());
        await _promotionQueue.Received(1).QueuePromotionAsync(Arg.Any<Guid>(), _staged!.TempObjectKey,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveText_sameTextAsTheParent_writesNothing()
    {
        var saved = await CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, "old text", _userId,
            TestContext.Current.CancellationToken);

        saved.Unchanged.Should().BeTrue();
        saved.VersionId.Should().Be(_parent.Id);
        _staged.Should().BeNull();
        await _s3.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveText_parentNoLongerCurrent_isRefusedBeforeAnythingIsWritten()
    {
        _file.CurrentVersionId = Guid.NewGuid();

        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, "# New text", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileVersionConflictException>();
        _staged.Should().BeNull();
        await _s3.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveText_anotherSaveLandsFirst_rollsBackAndAbandonsTheStagedCopy()
    {
        var lockedFile = new File
        {
            Id = _file.Id,
            Name = _file.Name,
            MimeType = Markdown,
            OwnerId = _userId,
            CurrentVersionId = Guid.NewGuid(),
        };
        _files.GetForUpdateAsync(_file.Id, _userId, Arg.Any<CancellationToken>()).Returns(lockedFile);

        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, "# New text", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileVersionConflictException>();
        _addedVersions.Should().BeEmpty();
        await _unitOfWork.Received().RollbackAsync(Arg.Any<CancellationToken>());
        await _uploads.Received(1).MarkInterruptedAsync(_staged!.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _s3.Received().DeleteObjectAsync(_config.TempBucket, $"content/{_staged.TempObjectKey}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveText_draftAtTheNamedRevision_isRemovedWithTheNewVersion()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        await CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, _draft.Revision, "# New text", _userId,
            TestContext.Current.CancellationToken);

        _addedVersions.Should().ContainSingle();
        _drafts.Received(1).Remove(_draft);
        await _unitOfWork.Received().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveText_draftAtAnotherRevision_isRefusedBeforeAnythingIsStaged()
    {
        _drafts.GetRevisionAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft.Revision);

        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, Guid.NewGuid(), "# New text", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileDraftConflictException>();
        _staged.Should().BeNull();
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    // The pre-check saw no draft, then one was written before the row lock was taken.
    [Fact]
    public async Task SaveText_draftWrittenAfterThePreCheck_isRefusedUnderTheLockAndKept()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, Guid.NewGuid(), "# New text", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileDraftConflictException>();
        _addedVersions.Should().BeEmpty();
        _drafts.DidNotReceive().Remove(_draft);
        await _unitOfWork.Received().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveText_draftButNoRevisionNamed_isRefused()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, "# New text", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileDraftConflictException>();
        _addedVersions.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveText_sameTextAsTheParent_stillRemovesTheDraft()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var saved = await CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, _draft.Revision, "old text", _userId,
            TestContext.Current.CancellationToken);

        saved.Unchanged.Should().BeTrue();
        _staged.Should().BeNull();
        _drafts.Received(1).Remove(_draft);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveText_encryptedParent_isRefused()
    {
        _parent.IsEncrypted = true;

        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, "# New text", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
        _staged.Should().BeNull();
    }

    [Fact]
    public async Task SaveText_nonTextParent_isRefused()
    {
        _parent.MimeType = "video/mp4";

        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, "# New text", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
        _staged.Should().BeNull();
    }

    [Fact]
    public async Task SaveText_parentTooLargeToOpen_isRefused()
    {
        _parent.Size = TextContentLimits.MaxBytes + 1;

        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, "# New text", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
        _staged.Should().BeNull();
    }

    [Fact]
    public async Task SaveText_tooLarge_isRefused()
    {
        var act = () => CreateSut().SaveTextContentAsync(_file.Id, _parent.Id, null, new string('a', 1_000_001), _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
        _staged.Should().BeNull();
    }

    [Fact]
    public async Task CreateTextFile_nameAlreadyUsedInTheFolder_isRefusedAndAbandoned()
    {
        var act = () => CreateSut().CreateTextFileAsync("notes.txt", _file.DirectoryId, string.Empty, _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileNameTakenException>();
        _addedVersions.Should().BeEmpty();
        await _uploads.Received(1).MarkInterruptedAsync(_staged!.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTextFile_freeName_createsTheFirstVersionAsMarkdown()
    {
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((File?)null);
        _files.CreateAsync(Arg.Any<File>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var file = call.Arg<File>();
                file.Id = Guid.NewGuid();
                return file;
            });

        var created = await CreateSut().CreateTextFileAsync("Untitled.md", _file.DirectoryId, string.Empty, _userId,
            TestContext.Current.CancellationToken);

        created.Created.Should().BeTrue();
        created.VersionNumber.Should().Be(1);
        created.MimeType.Should().Be(Markdown);
        _addedVersions.Should().ContainSingle().Which.Size.Should().Be(0);
    }

    [Fact]
    public async Task ReadText_freshSaveNotPromotedYet_readsTheStagedCopy()
    {
        var upload = new Upload { Id = Guid.NewGuid(), TempObjectKey = "staged-key", MimeType = Markdown };
        var contentObject = new ContentObject
        {
            Id = Guid.NewGuid(),
            Hash = _parent.ContentHash,
            StorageKey = "content/abc",
            IsPromoted = false,
            UploadId = upload.Id,
        };
        _parent.ContentObjectId = contentObject.Id;
        _contentObjects.GetByIdAsync(contentObject.Id, Arg.Any<CancellationToken>()).Returns(contentObject);
        _uploads.GetByIdAsync(upload.Id, Arg.Any<CancellationToken>()).Returns(upload);
        _s3.GetObjectAsync(_config.TempBucket, "content/staged-key", Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = new MemoryStream("old text"u8.ToArray()) });

        var text = await CreateSut().ReadVersionTextAsync(_parent.Id, _userId, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(text.Content).Should().Be("old text");
        text.MimeType.Should().Be(Markdown);
        await _s3.DidNotReceive().GetObjectAsync(_config.UploadBucket, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadText_encryptedVersion_isRefused()
    {
        _parent.IsEncrypted = true;

        var act = () => CreateSut().ReadVersionTextAsync(_parent.Id, _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
    }
}
