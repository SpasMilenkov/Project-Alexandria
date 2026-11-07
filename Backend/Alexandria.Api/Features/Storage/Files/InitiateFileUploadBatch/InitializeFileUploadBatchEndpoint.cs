using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Features.Storage.Files.InitiateFileUpload;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.InitiateFileUploadBatch;

internal sealed class InitializeFileUploadBatchRequest
{
    public List<InitializeFileUploadRequest> Items { get; set; } = [];
}

internal sealed class InitializeFileUploadBatchResult
{
    public Guid? UploadId { get; set; }
    public string? UploadUrl { get; set; }
    public string? Error { get; set; }
}

internal sealed class InitializeFileUploadBatchResponse
{
    public List<InitializeFileUploadBatchResult> Results { get; set; } = [];
}

sealed class InitializeFileUploadBatchRequestValidator : Validator<InitializeFileUploadBatchRequest>
{
    public const int MaxItems = 100;

    public InitializeFileUploadBatchRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one upload must be provided.");

        RuleFor(x => x.Items)
            .Must(items => items is not null && items.Count <= MaxItems)
            .WithMessage($"At most {MaxItems} uploads can be started at once.");

        RuleForEach(x => x.Items)
            .NotNull()
            .WithMessage("Uploads cannot be null.");
    }
}

sealed class InitializeFileUploadBatchEndpoint(IContentStorageService storage)
    : Endpoint<InitializeFileUploadBatchRequest, InitializeFileUploadBatchResponse>
{
    // Item rules run here rather than in the request validator, which would reject the whole batch.
    private static readonly InitializeFileUploadRequestValidator ItemValidator = new();

    public override void Configure()
    {
        Post("files/init-upload/batch");
        Policies(Common.Auth.Policies.CanUpload);
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Start several uploads";
            s.Description = "Creates an upload and a signed URL for each item. An invalid item is "
                            + "reported in its own result and doesn't affect the others. Results are "
                            + "in the same order as the items.";
            s.Responses[200] = "Batch processed, see per-item results";
            s.Responses[400] = "Bad request - no items, too many items, or malformed JSON";
        });
    }

    public override async Task HandleAsync(InitializeFileUploadBatchRequest req, CancellationToken ct)
    {
        var results = new InitializeFileUploadBatchResult[req.Items.Count];
        var validIndexes = new List<int>(req.Items.Count);

        for (var i = 0; i < req.Items.Count; i++)
        {
            var validation = ItemValidator.Validate(req.Items[i]);

            if (validation.IsValid)
                validIndexes.Add(i);
            else
                results[i] = new InitializeFileUploadBatchResult { Error = validation.Errors[0].ErrorMessage };
        }

        if (validIndexes.Count > 0)
        {
            var created = await storage.InitiateFileUploads(
                validIndexes.Select(i => ToItem(req.Items[i])).ToList(),
                User.GetUserId(),
                ct);

            for (var i = 0; i < validIndexes.Count; i++)
            {
                results[validIndexes[i]] = new InitializeFileUploadBatchResult
                {
                    UploadId = created[i].UploadId,
                    UploadUrl = created[i].UploadUrl,
                    Error = created[i].Error
                };
            }
        }

        await Send.OkAsync(new InitializeFileUploadBatchResponse { Results = results.ToList() }, ct);
    }

    private static UploadInitItem ToItem(InitializeFileUploadRequest item) => new()
    {
        ContentType = item.ContentType,
        Hash = item.Hash,
        ContentLength = item.ContentLength,
        DirectoryId = item.DirectoryId
    };
}
