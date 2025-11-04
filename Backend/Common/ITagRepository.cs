using System.Linq.Expressions;
using DTO;
using Models;

namespace Common;

public interface ITagRepository : IRepository<Tag>
{
    Task<Tag> CreateAsync(Tag tag, CancellationToken ct = default);
    Task<Tag> UpdateAsync(Tag tag, CancellationToken ct = default);
    
    // Tag-specific methods
    Task<IEnumerable<Tag>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Tag?> GetByNameAndUserIdAsync(string name, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Tag>> GetTagsWithFilesAsync(Guid userId, CancellationToken ct = default);
    Task<(IEnumerable<Tag> Tags, int TotalCount)> FindTagsAsync(
        TagSearchQuery query, 
        CancellationToken ct = default);
}