using Alexandria.Data.Models;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Common.Builders;

public class FileVersionBuilder
{
    private Guid _id = Guid.NewGuid();
    private byte[] _contentHash = new byte[32];
    private long _size = 1024L;
    private int _versionNumber = 1;
    private string _mimeType = "text/plain";
    private bool _isEncrypted = false;
    private byte[]? _encryptionIv = null;
    private byte[]? _encryptionSalt = null;
    private byte[]? _integrityTag = null;
    private string? _encryptionHint = null;
    private short? _kdfVersion = null;
    private int? _iterationCount = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt = null;
    private DateTime? _deletedAt = null;
    private Guid _createdBy = Guid.NewGuid();
    private Guid? _updatedBy = null;
    private ContentObject? _contentObject = null;
    private Guid _contentObjectId = Guid.Empty;
    private Guid _fileId = Guid.Empty;
    private File? _file = null;

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

    public FileVersionBuilder WithIsEncrypted(bool isEncrypted)
    {
        _isEncrypted = isEncrypted;
        return this;
    }

    public FileVersionBuilder WithEncryptionIv(byte[]? encryptionIv)
    {
        _encryptionIv = encryptionIv;
        return this;
    }

    public FileVersionBuilder WithEncryptionSalt(byte[]? encryptionSalt)
    {
        _encryptionSalt = encryptionSalt;
        return this;
    }

    public FileVersionBuilder WithIntegrityTag(byte[]? integrityTag)
    {
        _integrityTag = integrityTag;
        return this;
    }

    public FileVersionBuilder WithEncryptionHint(string? encryptionHint)
    {
        _encryptionHint = encryptionHint;
        return this;
    }

    public FileVersionBuilder WithKdfVersion(short? kdfVersion)
    {
        _kdfVersion = kdfVersion;
        return this;
    }

    public FileVersionBuilder WithIterationCount(int? iterationCount)
    {
        _iterationCount = iterationCount;
        return this;
    }

    public FileVersionBuilder WithUpdatedAt(DateTime? updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public FileVersionBuilder WithDeletedAt(DateTime? deletedAt)
    {
        _deletedAt = deletedAt;
        return this;
    }

    public FileVersionBuilder WithUpdatedBy(Guid? updatedBy)
    {
        _updatedBy = updatedBy;
        return this;
    }

    public FileVersionBuilder WithContentObjectId(Guid contentObjectId)
    {
        _contentObjectId = contentObjectId;
        return this;
    }

    public FileVersionBuilder WithFileId(Guid fileId)
    {
        _fileId = fileId;
        return this;
    }

    public FileVersionBuilder WithFile(File file)
    {
        _file = file;
        return this;
    }

    public FileVersion Build() => new()
    {
        Id = _id,
        ContentHash = _contentHash,
        Size = _size,
        VersionNumber = _versionNumber,
        MimeType = _mimeType,
        IsEncrypted = _isEncrypted,
        EncryptionIv = _encryptionIv,
        EncryptionSalt = _encryptionSalt,
        IntegrityTag = _integrityTag,
        EncryptionHint = _encryptionHint,
        KdfVersion = _kdfVersion,
        IterationCount = _iterationCount,
        CreatedAt = _createdAt,
        CreatedBy = _createdBy,
        UpdatedAt = _updatedAt,
        DeletedAt = _deletedAt,
        UpdatedBy = _updatedBy,
        ContentObject = _contentObject!,
        ContentObjectId = _contentObjectId,
        FileId = _fileId,
        File = _file!,
    };
}