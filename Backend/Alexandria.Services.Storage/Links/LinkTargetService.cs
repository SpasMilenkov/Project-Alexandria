using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;

namespace Alexandria.Services.Storage.Links;

public sealed class LinkTargetService(IUnitOfWork unitOfWork) : ILinkTargetService
{
    public async Task<IReadOnlyList<LinkTargetDto>> ResolveAsync(IReadOnlyCollection<LinkTargetRef> targets,
        Guid userId, CancellationToken ct = default)
    {
        var requested = targets.Distinct().ToList();
        var found = new Dictionary<LinkTargetRef, LinkTargetDto>();

        // One query per kind; the unit of work shares a single context, so they run one after another.
        foreach (var group in requested.GroupBy(target => target.Kind))
        {
            var ids = group.Select(target => target.Id).ToList();
            var rows = await LoadAsync(group.Key, ids, userId, ct);

            foreach (var row in rows)
                found[new LinkTargetRef(row.Kind, row.Id)] = row;
        }

        return requested
            .Select(target => found.GetValueOrDefault(target) ?? Unavailable(target))
            .ToList();
    }

    private Task<IReadOnlyList<LinkTargetDto>> LoadAsync(LinkTargetKind kind, IReadOnlyCollection<Guid> ids,
        Guid userId, CancellationToken ct) =>
        kind switch
        {
            LinkTargetKind.File => unitOfWork.Files.GetLinkTargetsAsync(ids, userId, ct),
            LinkTargetKind.Folder => unitOfWork.Directories.GetLinkTargetsAsync(ids, userId, ct),
            LinkTargetKind.Tag => unitOfWork.Tags.GetLinkTargetsAsync(ids, userId, ct),
            LinkTargetKind.Playlist => unitOfWork.Playlists.GetLinkTargetsAsync(ids, userId, ct),
            _ => Task.FromResult<IReadOnlyList<LinkTargetDto>>([]),
        };

    private static LinkTargetDto Unavailable(LinkTargetRef target) => new()
    {
        Kind = target.Kind,
        Id = target.Id,
        State = LinkTargetState.Unavailable,
    };
}
