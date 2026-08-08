using System.Linq.Expressions;
using Alexandria.Data.Models;
using Alexandria.Dto.Tags;

namespace Alexandria.Repositories.Projections;

public static class TagProjections
{
    /// <summary>
    /// Tag → TagDto. The system id is an argument so EF parameterizes the comparison
    /// and the projection stays testable in memory.
    /// </summary>
    public static Expression<Func<Tag, TagDto>> ToTagDto(Guid systemId) =>
        t => new TagDto
        {
            Id = t.Id,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            Name = t.Name,
            Color = t.Color,
            Icon = t.Icon,
            Description = t.Description,
            UserId = t.OwnerId,
            IsSystem = t.OwnerId == systemId,
            Facet = t.Facet,
            ParentId = t.ParentId,
            ParentName = t.Parent == null ? null : t.Parent.Name
        };
}