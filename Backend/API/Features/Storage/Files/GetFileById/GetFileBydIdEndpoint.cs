using API.Features.Auth.Extensions;
using Common.Services;
using DTO.Files;
using FastEndpoints;

namespace API.Features.Storage.Files.GetFileById;

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

        await Send.OkAsync(await fileService.GetFileWithOwnershipById(fileId: req.Id, userId: userId, ct), ct);
    }
}