using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;

namespace Alexandria.Common.Services;

/// <summary>
/// Keeps the index of what notes link to, and answers "which notes mention this". Saves from the
/// editor are indexed right away; uploads, restores of unread versions and notes moved into Notes
/// are picked up by a sweep.
/// </summary>
public interface INoteLinkIndexService
{
    /// <summary>
    /// Indexes the text of a version the caller already holds. Never throws: the version is saved
    /// either way, and the sweep reads it later.
    /// </summary>
    Task IndexVersionAsync(Guid versionId, string markdown, CancellationToken ct = default);

    /// <summary>Reads a note's text from storage and indexes it.</summary>
    /// <exception cref="Exception">Storage failed in a way that would hit other notes too.</exception>
    Task IndexCandidateAsync(NoteLinkScanCandidate candidate, CancellationToken ct = default);

    /// <summary>The viewer's own notes that link to the target, most recently edited first.</summary>
    Task<IReadOnlyList<NoteBacklinkDto>> GetBacklinksAsync(LinkTargetKind kind, Guid targetId, Guid userId,
        CancellationToken ct = default);
}
