using Alexandria.Data.Models;
using Bogus;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Common.Builders;

public class FileBuilder
{
    private static readonly Faker Faker = new();

    private Guid _id = Guid.NewGuid();
    private string _name = Faker.System.FileName("txt");
    private string _mimeType = "text/plain";
    private Guid _ownerId = Guid.NewGuid();
    private Guid? _directoryId = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _deletedAt = null;
    private bool _hasPreview = false;
    private FileVersion? _currentVersion = null;
    private Guid _previewId = Guid.Empty;
    private Preview? _preview = null;

    public FileBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public FileBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public FileBuilder WithMimeType(string mimeType)
    {
        _mimeType = mimeType;
        return this;
    }

    public FileBuilder WithOwner(Guid ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    public FileBuilder WithDirectory(Guid? directoryId)
    {
        _directoryId = directoryId;
        return this;
    }

    public FileBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public FileBuilder WithDeletedAt(DateTime? deletedAt)
    {
        _deletedAt = deletedAt;
        return this;
    }

    public FileBuilder WithHasPreview(bool hasPreview)
    {
        _hasPreview = hasPreview;
        return this;
    }

    public FileBuilder WithCurrentVersion(FileVersion version)
    {
        _currentVersion = version;
        return this;
    }

    public FileBuilder WithPreview(Preview preview)
    {
        _preview = preview;
        _previewId = preview.Id;
        _hasPreview = true;
        return this;
    }

    public FileBuilder WithPreviewId(Guid id)
    {
        _previewId = id;
        _hasPreview = true;
        return this;
    }

    public File Build() => new()
    {
        Id = _id,
        Name = _name,
        MimeType = _mimeType,
        OwnerId = _ownerId,
        DirectoryId = _directoryId,
        CreatedAt = _createdAt,
        DeletedAt = _deletedAt,
        HasPreview = _hasPreview,
        CurrentVersion = _currentVersion!,
        PreviewId = _previewId,
        Preview = _preview,
        NormalizedName = _name.ToLowerInvariant(),
        // SearchVector is a DB-computed column — not set for in-memory test use
        Tags = new List<Tag>(),
        SignedUrls = new List<SignedUrl>()
    };
}