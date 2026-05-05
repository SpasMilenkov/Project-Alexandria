using Alexandria.Data.Models;

namespace Alexandria.Tests.Common.Builders;

public class FileVersionBuilder
{
    private Guid _id = Guid.NewGuid();
    private byte[] _contentHash = new byte[32];
    private long _size = 1024L;
    private int _versionNumber = 1;
    private string _mimeType = "text/plain";
    private DateTime _createdAt = DateTime.UtcNow;
    private Guid _createdBy = Guid.NewGuid();
    private ContentObject? _contentObject = null;

    // FileId is intentionally left unset — must be assigned after the File is persisted
    // to break the circular FK between File.CurrentVersionId and FileVersion.FileId.

    public FileVersionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public FileVersionBuilder WithContentHash(byte[] hash)
    {
        _contentHash = hash;
        return this;
    }

    public FileVersionBuilder WithSize(long size)
    {
        _size = size;
        return this;
    }

    public FileVersionBuilder WithVersionNumber(int versionNumber)
    {
        _versionNumber = versionNumber;
        return this;
    }

    public FileVersionBuilder WithMimeType(string mimeType)
    {
        _mimeType = mimeType;
        return this;
    }

    public FileVersionBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public FileVersionBuilder WithCreatedBy(Guid userId)
    {
        _createdBy = userId;
        return this;
    }

    public FileVersionBuilder WithContentObject(ContentObject co)
    {
        _contentObject = co;
        return this;
    }

    public FileVersion Build() => new()
    {
        Id = _id,
        ContentHash = _contentHash,
        Size = _size,
        VersionNumber = _versionNumber,
        MimeType = _mimeType,
        CreatedAt = _createdAt,
        CreatedBy = _createdBy,
        ContentObject = _contentObject!,
    };
}