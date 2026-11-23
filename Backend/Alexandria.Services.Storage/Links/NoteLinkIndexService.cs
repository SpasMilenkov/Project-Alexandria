using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Storage.Links;

public sealed partial class NoteLinkIndexService(
    IUnitOfWork unitOfWork,
    IContentStorageService contentStorage,
    ILogger<NoteLinkIndexService> logger)
    : INoteLinkIndexService
{
    // A drawer section, not a report; past this many the newest are what matter.
    internal const int BacklinkLimit = 100;

    // Stored text never carries a byte order mark, but an uploaded file may.
    private static readonly UTF8Encoding TextEncoding = new(encoderShouldEmitUTF8Identifier: false);

    public async Task IndexVersionAsync(Guid versionId, string markdown, CancellationToken ct = default)
    {
        try
        {
            var version = await unitOfWork.FileVersions.GetByIdAsync(versionId, ct);

            if (version is null || version.IsEncrypted) return;

            await IndexContentAsync(version.ContentObjectId, markdown, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogIndexFailed(logger, ex, versionId);
        }
    }

    public async Task IndexCandidateAsync(NoteLinkScanCandidate candidate, CancellationToken ct = default)
    {
        // The editor's save may have read the same text since the batch was listed.
        if (await IsCurrentAsync(candidate.ContentObjectId, ct)) return;

        string markdown;

        try
        {
            var text = await contentStorage.ReadVersionTextAsync(candidate.VersionId, candidate.OwnerId, ct);

            markdown = TextEncoding.GetString(text.Content).TrimStart('﻿');
        }
        catch (Exception ex) when (ex is FileVersionNotFoundException or FileContentNotEditableException)
        {
            // Deleted, encrypted or grown too large since the batch was listed; the next listing
            // no longer offers it.
            LogCandidateMovedOn(logger, candidate.VersionId);

            return;
        }
        catch (ContentObjectNotFoundException ex)
        {
            // The bytes are gone for good. An empty scan keeps the sweep from asking again forever.
            LogContentMissing(logger, ex, candidate.ContentObjectId);
            markdown = string.Empty;
        }

        await IndexContentAsync(candidate.ContentObjectId, markdown, ct);
    }

    public Task<IReadOnlyList<NoteBacklinkDto>> GetBacklinksAsync(LinkTargetKind kind, Guid targetId, Guid userId,
        CancellationToken ct = default) =>
        unitOfWork.NoteLinks.GetBacklinksAsync(kind, targetId, userId, BacklinkLimit, ct);

    private async Task IndexContentAsync(Guid contentObjectId, string markdown, CancellationToken ct)
    {
        // Content is shared by identical texts, so it is often read already.
        if (await IsCurrentAsync(contentObjectId, ct)) return;

        var links = NoteLinkExtractor.Extract(markdown);

        var scan = new NoteLinkScan
        {
            ContentObjectId = contentObjectId,
            ScannerVersion = NoteLinkExtractor.CurrentVersion,
            Links = links
                .Select(link => new NoteLink
                {
                    ContentObjectId = contentObjectId,
                    TargetKind = link.Kind,
                    TargetId = link.TargetId,
                    Snippet = link.Snippet,
                })
                .ToList(),
        };

        if (await unitOfWork.NoteLinks.ReplaceAsync(scan, ct))
            LogIndexed(logger, contentObjectId, links.Count);
        else
            LogIndexedElsewhere(logger, contentObjectId);
    }

    private async Task<bool> IsCurrentAsync(Guid contentObjectId, CancellationToken ct)
    {
        var version = await unitOfWork.NoteLinks.GetScannerVersionAsync(contentObjectId, ct);

        return version >= NoteLinkExtractor.CurrentVersion;
    }
}
