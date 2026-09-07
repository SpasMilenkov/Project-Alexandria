namespace Alexandria.Api.Features.Admin.Storage;

public sealed class GetUserStorageRankingRequest
{
    public int Top { get; set; } = 10;
}