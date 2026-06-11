using System.Security.Cryptography;
using Alexandria.Common;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Exceptions.SignedUrls;
using Alexandria.Common.Services;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.SignedUrls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.SignedUrls;

public sealed partial class SignedUrlService(
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    ILogger<SignedUrlService> logger,
    AlexandriaDbContext context) : ISignedUrlService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(7);
    private static readonly TimeSpan DownloadUrlValidity = TimeSpan.FromMinutes(5);

    public async Task<CreateShareLinkResponse> CreateShareLinkAsync(
        Guid fileId,
        string userId,
        TimeSpan? expiry,
        Guid? fileVersionId = null,
        int? maxAccessCount = null,
        CancellationToken ct = default)
    {
        var fileExists = await context.Files
            .AnyAsync(f => f.Id == fileId && f.OwnerId.ToString() == userId && f.DeletedAt == null, ct);

        if (!fileExists)
            throw new FileNotFoundException($"File {fileId} not found for user {userId}.");

        if (fileVersionId.HasValue)
        {
            var versionBelongsToFile = await context.FileVersions
                .AnyAsync(v =>
                    v.Id == fileVersionId.Value &&
                    v.FileId == fileId &&
                    v.DeletedAt == null, ct);

            if (!versionBelongsToFile)
                throw new FileVersionNotFoundException(fileVersionId.Value);
        }

        var token = GenerateToken();
        var now = DateTime.UtcNow;

        var signedUrl = new SignedUrl
        {
            FileId = fileId,
            Token = token,
            CreatorId = userId,
            ExpiresAt = now.Add(expiry ?? DefaultExpiry),
            FileVersionId = fileVersionId,
            MaxAccessCount = maxAccessCount
        };

        LogCreatingShareLink(fileId, userId, fileVersionId);
        var created = await unitOfWork.SignedUrls.CreateAsync(signedUrl, ct);
        return CreateShareLinkResponse.FromEntity(created);
    }

    public async Task<SharedFileMetadataDto> GetSharedFileMetadataAsync(
        string token,
        CancellationToken ct = default)
    {
        var signedUrl = await ValidateTokenAsync(token, ct);
        var version = await ResolveVersionAsync(signedUrl, ct);
        return SharedFileMetadataDto.FromEntities(signedUrl, version);
    }

    public async Task<ShareDownloadResponse> GetDownloadUrlAsync(
        string token,
        CancellationToken ct = default)
    {
        var signedUrl = await ValidateTokenAsync(token, ct);
        var version = await ResolveVersionAsync(signedUrl, ct);

        if (signedUrl.AccessCount >= signedUrl.MaxAccessCount)
            throw
                new SignedUrlExpiredException(token);

        LogGeneratingDownloadUrl(signedUrl.FileId, signedUrl.FileVersionId, token);

        var presignedUrl = await storageService.GetFilePresignedUrl(
            signedUrl.FileId,
            version.ContentHash,
            signedUrl.FileInfo.Name,
            DownloadUrlValidity);

        await unitOfWork.SignedUrls.IncrementAccessCountAsync(signedUrl.Id, ct);

        return new ShareDownloadResponse
        {
            PresignedUrl = presignedUrl,
            FileName = signedUrl.FileInfo.Name,
            MimeType = version.MimeType,
        };
    }

    public async Task<IEnumerable<ShareLinkSummaryDto>> GetShareLinksForFileAsync(
        Guid fileId,
        string userId,
        CancellationToken ct = default)
    {
        var fileExists = await context.Files
            .AnyAsync(f => f.Id == fileId && f.OwnerId.ToString() == userId && f.DeletedAt == null, ct);

        if (!fileExists)
            throw new FileNotFoundException($"File {fileId} not found for user {userId}.");

        var links = await unitOfWork.SignedUrls.GetByFileIdAsync(fileId, ct);
        return links.Select(ShareLinkSummaryDto.FromEntity);
    }

    public async Task<bool> RevokeShareLinkAsync(Guid id, string userId, CancellationToken ct = default)
    {
        LogRevokingShareLink(id, userId);
        return await unitOfWork.SignedUrls.RevokeAsync(id, userId, ct);
    }

    private async Task<SignedUrl> ValidateTokenAsync(string token, CancellationToken ct)
    {
        var signedUrl = await unitOfWork.SignedUrls.GetByTokenAsync(token, ct)
                        ?? throw new SignedUrlNotFoundException(token);

        if (signedUrl.ExpiresAt < DateTime.UtcNow || (signedUrl.MaxAccessCount != null &&
                                                      signedUrl.MaxAccessCount.Value < signedUrl.AccessCount))
            throw new SignedUrlExpiredException(token);
        return signedUrl;
    }

    /// <summary>
    /// Resolves the correct FileVersion for a signed URL.
    /// When the link has a pinned version, that version is fetched and cross-checked against the file id.
    /// Otherwise the file's current version pointer is followed.
    /// </summary>
    private async Task<FileVersion> ResolveVersionAsync(SignedUrl signedUrl, CancellationToken ct)
    {
        if (signedUrl.FileVersionId is not null)
        {
            return await context.FileVersions
                       .Include(v => v.ContentObject)
                       .Where(v =>
                           v.Id == signedUrl.FileVersionId &&
                           v.FileId == signedUrl.FileId &&
                           v.DeletedAt == null &&
                           v.ContentObject.DeletedAt == null &&
                           v.ContentObject.OrphanedAt == null)
                       .FirstOrDefaultAsync(ct)
                   ?? throw new FileVersionNotFoundException(signedUrl.FileVersionId.Value);
        }

        return await context.FileVersions
                   .Include(v => v.ContentObject)
                   .Where(v =>
                       v.FileId == signedUrl.FileId &&
                       v.File.CurrentVersionId == v.Id &&
                       v.DeletedAt == null &&
                       v.ContentObject.DeletedAt == null &&
                       v.ContentObject.OrphanedAt == null)
                   .FirstOrDefaultAsync(ct)
               ?? throw new FileNotFoundException($"No active version found for file {signedUrl.FileId}.");
    }

    //Could use webencoder here but don't want to bring the dependency
    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}