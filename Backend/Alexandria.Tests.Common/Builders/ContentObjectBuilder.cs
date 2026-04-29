using System.Security.Cryptography;
using Alexandria.Data.Models;

namespace Alexandria.Tests.Common.Builders;

public class ContentObjectBuilder
{
    private Guid _id = Guid.NewGuid();

    // Generate a random hash by default — prevents unique constraint violations
    // when multiple ContentObjects are seeded in the same test run
    private byte[] _hash = RandomNumberGenerator.GetBytes(32);
    private string? _storageKey = null; // derived from hash lazily in Build()
    private bool _isPromoted = false;
    private DateTime? _promotedAt = null;
    private readonly int _promotionAttempts = 0;
    private DateTime? _lastPromotionAttemptAt = null;
    private DateTime? _orphanedAt = null;
    private Guid? _uploadId = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _deletedAt = null;

    public ContentObjectBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ContentObjectBuilder WithHash(byte[] hash)
    {
        _hash = hash;
        return this;
    }

    /// <summary>
    /// Derives the hash from actual content bytes (SHA256) and sets the StorageKey
    /// to match the real service convention ("content/{hex}"). Use this when testing
    /// deduplication — two calls with identical <paramref name="content"/> produce the
    /// same hash and storage key.
    /// </summary>
    public ContentObjectBuilder WithContentData(byte[] content)
    {
        _hash = SHA256.HashData(content);
        _storageKey = $"content/{Convert.ToHexString(_hash).ToLowerInvariant()}";
        return this;
    }

    public ContentObjectBuilder WithStorageKey(string storageKey)
    {
        _storageKey = storageKey;
        return this;
    }

    public ContentObjectBuilder WithIsPromoted(bool isPromoted)
    {
        _isPromoted = isPromoted;
        return this;
    }

    public ContentObjectBuilder WithPromotedAt(DateTime? promotedAt)
    {
        _promotedAt = promotedAt;
        return this;
    }

    public ContentObjectBuilder WithUpload(Guid? uploadId)
    {
        _uploadId = uploadId;
        return this;
    }

    public ContentObjectBuilder WithOrphanedAt(DateTime? orphanedAt)
    {
        _orphanedAt = orphanedAt;
        return this;
    }

    public ContentObjectBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public ContentObjectBuilder WithDeletedAt(DateTime? deletedAt)
    {
        _deletedAt = deletedAt;
        return this;
    }

    public ContentObject Build()
    {
        var hash = _hash;
        var storageKey = _storageKey ?? $"content/{Convert.ToHexString(hash).ToLowerInvariant()}";

        return new ContentObject
        {
            Id = _id,
            Hash = hash,
            StorageKey = storageKey,
            IsPromoted = _isPromoted,
            PromotedAt = _promotedAt,
            PromotionAttempts = _promotionAttempts,
            LastPromotionAttemptAt = _lastPromotionAttemptAt,
            OrphanedAt = _orphanedAt,
            UploadId = _uploadId,
            CreatedAt = _createdAt,
            DeletedAt = _deletedAt
        };
    }
}