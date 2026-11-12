using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Api.Features.Activity.GetEntityHistory;

sealed class GetEntityHistoryRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
