using Alexandria.Data.Models;
using Alexandria.Dto.Links;
using Alexandria.Dto.Tags;

namespace Alexandria.Common.Repositories;

public interface ITagRepository : IRepository<Tag>
{
    Task<Tag> CreateAsync(Tag tag, CancellationToken ct = default);

    Task<Tag> UpdateAsync(Tag tag, CancellationToken ct = default);

    // Tag-specific methods
    Task<Tag?> GetByIdAndUserIdAsync(Guid userId, Guid tagId, CancellationToken ct = default);
    Task<Tag?> GetByNameAndUserIdAsync(string name, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Link targets for the live tags among <paramref name="tagIds"/> that <paramref name="userId"/>
    /// can use: their own and the system vocabulary.
    /// </summary>
    Task<IReadOnlyList<LinkTargetDto>> GetLinkTargetsAsync(IReadOnlyCollection<Guid> tagIds, Guid userId,
        CancellationToken ct = default);

    Task<(IEnumerable<TagDto> Tags, int TotalCount)> FindTagsAsync(TagSearchQuery query,
        CancellationToken ct = default);
}