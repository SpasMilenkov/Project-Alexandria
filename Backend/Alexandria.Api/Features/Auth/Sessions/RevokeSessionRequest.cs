namespace Alexandria.Api.Features.Auth.Sessions;

sealed class RevokeSessionRequest
{
    public Guid Id { get; set; }
}
