using FastEndpoints;
using Models.Enumerators;

namespace API.Features.Users.GetUserActivity;

sealed class GetUserActivtyRequest
{
    public Guid UserId { get; set; }
    
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public SortBy SortBy { get; set; } = SortBy.CreatedAt;
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}

sealed class GetUserActivtyResponse
{

}

sealed class GetUserActivtyEndpoint : Endpoint<GetUserActivtyRequest, GetUserActivtyResponse>
{
    public override void Configure()
    {
        Get("users/activity");
        
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetUserActivtyRequest r, CancellationToken c)
    {
        
    }
}