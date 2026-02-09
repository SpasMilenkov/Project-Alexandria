using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.InitiateFileUpload;

sealed class InitializeFileUploadRequest
{
    public required string ContentType { get; set; }
    public required string Sha256 { get; set; }
    public required long ContentLength { get; set; }
    public Guid? DirectoryId { get; set; }
}

sealed class InitializeFileUploadResponse
{
    public Guid UploadId { get; set; }
    public required string UploadUrl { get; set; }
}

sealed class InitializeFileUploadEndpoint(IStorageService s3Service)
    : Endpoint<InitializeFileUploadRequest, InitializeFileUploadResponse>
{
    public override void Configure()
    {
        Post("files/init-upload");
    }

    public override async Task HandleAsync(InitializeFileUploadRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await s3Service.InitiateFileUpload(
            req.ContentType,
            req.Sha256,
            userId,
            req.ContentLength,
            req.DirectoryId, ct);
        await Send.OkAsync(
            new InitializeFileUploadResponse
            {
                UploadId = result.Item1,
                UploadUrl = result.Item2
            },
            ct
        );
    }
}