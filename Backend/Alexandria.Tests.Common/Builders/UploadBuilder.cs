using System.Numerics;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Tests.Common.Builders;

public class UploadBuilder
{
    private Guid _id = Guid.NewGuid();
    private UploadStatus _status = UploadStatus.InProgress;
    private byte[] _hash = new byte[32]; // 32 zero bytes — valid SHA-256 length
    private string _tempObjectKey = $"temp/{Guid.NewGuid()}";
    private BigInteger _size = BigInteger.Zero;
    private string _mimeType = "text/plain";
    private readonly DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt = null;
    private DateTime? _deletedAt = null;
    private Guid? _updatedBy = null;
    private Guid _userId = Guid.NewGuid();
    private ApplicationUser? _user = null;
    private DateTime? _finishedAt = null;

    public UploadBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public UploadBuilder WithStatus(UploadStatus status)
    {
        _status = status;
        return this;
    }

    public UploadBuilder WithHash(byte[] hash)
    {
        _hash = hash;
        return this;
    }

    public UploadBuilder WithUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public UploadBuilder WithTempObjectKey(string key)
    {
        _tempObjectKey = key;
        return this;
    }

    public UploadBuilder WithSize(BigInteger size)
    {
        _size = size;
        return this;
    }

    public UploadBuilder WithMimeType(string mimeType)
    {
        _mimeType = mimeType;
        return this;
    }

    public UploadBuilder WithFinishedAt(DateTime? finishedAt)
    {
        _finishedAt = finishedAt;
        return this;
    }

    public UploadBuilder WithUpdatedAt(DateTime? updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public UploadBuilder WithDeletedAt(DateTime? deletedAt)
    {
        _deletedAt = deletedAt;
        return this;
    }

    public UploadBuilder WithUpdatedBy(Guid? updatedBy)
    {
        _updatedBy = updatedBy;
        return this;
    }

    public UploadBuilder WithUser(ApplicationUser user)
    {
        _user = user;
        return this;
    }

    public Upload Build() => new()
    {
        Id = _id,
        Status = _status,
        Hash = _hash,
        TempObjectKey = _tempObjectKey,
        Size = _size,
        MimeType = _mimeType,
        CreatedAt = _createdAt,
        UpdatedAt = _updatedAt,
        DeletedAt = _deletedAt,
        UpdatedBy = _updatedBy,
        UserId = _userId,
        User = _user!,
        FinishedAt = _finishedAt
    };
}