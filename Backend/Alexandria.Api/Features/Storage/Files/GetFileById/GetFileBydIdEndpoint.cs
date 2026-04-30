using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.GetFileById;

sealed class GetFileByIdRequest
{
    public Guid Id { get; set; }
}

sealed class GetFileByIdEndpoint(IFileService fileService) : Endpoint<GetFileByIdRequest, FileResult>
{
    public override void Configure()
    {
        Get("files/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetFileByIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await fileService.GetFileWithOwnershipByIdAsync(fileId: req.Id, userId: userId, ct), ct);
    }
}