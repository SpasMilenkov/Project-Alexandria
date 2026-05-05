using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.GetUserActivity;

sealed class GetUserActivityRequest
{
    public Guid UserId { get; set; }

    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public SortBy SortBy { get; set; } = SortBy.CreatedAt;
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}

sealed class GetUserActivityEndpoint : Endpoint<GetUserActivityRequest, PaginatedResult<AuditLogResult>>
{
    public override void Configure()
    {
        Get("users/activity");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(GetUserActivityRequest req, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}