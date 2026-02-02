using DTO.Tags;
using Models;

namespace Common.Repositories;

public interface ITagRepository : IRepository<Tag>
{
    Task<Tag> CreateAsync(Tag tag, CancellationToken ct = default);

    Task<Tag> UpdateAsync(Tag tag, CancellationToken ct = default);

    // Tag-specific methods
    Task<Tag?> GetByIdAndUserIdAsync(Guid userId, Guid tagId, CancellationToken ct = default);
    Task<Tag?> GetByNameAndUserIdAsync(string name, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<TagDto>> GetTagsWithFilesAsync(Guid userId, CancellationToken ct = default);

    Task<(IEnumerable<TagDto> Tags, int TotalCount)> FindTagsAsync(TagSearchQuery query,
        CancellationToken ct = default);
}