using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Files.InitiateFileUpload;

sealed class InitializeFileUploadRequest
{
    public required string ContentType { get; set; }
    public required string Hash { get; set; }
    public required long ContentLength { get; set; }
    public Guid? DirectoryId { get; set; }
}

sealed class InitializeFileUploadResponse
{
    public Guid UploadId { get; set; }
    public required string UploadUrl { get; set; }
}

sealed class InitializeFileUploadRequestValidator : Validator<InitializeFileUploadRequest>
{
    public InitializeFileUploadRequestValidator()
    {
        RuleFor(x => x.ContentType)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Content type is required.");

        RuleFor(x => x.Hash)
            .NotEmpty()
            .Length(64)
            .Matches("^[a-fA-F0-9]+$")
            .WithMessage("Hash must be a valid 64-character hexadecimal string.");

        RuleFor(x => x.ContentLength)
            .GreaterThan(0)
            .LessThanOrEqualTo(100 * 1024 * 1024) // 100MB example limit
            .WithMessage("File size exceeds the allowed limit.");

        RuleFor(x => x.DirectoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.DirectoryId.HasValue)
            .WithMessage("DirectoryId cannot be an empty GUID.");
    }
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
            req.Hash,
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