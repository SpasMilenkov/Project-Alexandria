using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Features.Storage.Files.FinalizeFileUpload;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.FinalizeFileUploadBatch;

internal sealed class FinalizeFileUploadBatchRequest
{
    public List<FinalizeFileUploadRequest> Items { get; set; } = [];
}

internal sealed class FinalizeFileUploadBatchResult
{
    public Guid UploadId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

internal sealed class FinalizeFileUploadBatchResponse
{
    public List<FinalizeFileUploadBatchResult> Results { get; set; } = [];
}

sealed class FinalizeFileUploadBatchRequestValidator : Validator<FinalizeFileUploadBatchRequest>
{
    public const int MaxItems = 100;

    public FinalizeFileUploadBatchRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one upload must be provided.");

        RuleFor(x => x.Items)
            .Must(items => items is not null && items.Count <= MaxItems)
            .WithMessage($"At most {MaxItems} uploads can be finalized at once.");

        RuleForEach(x => x.Items)
            .NotNull()
            .WithMessage("Uploads cannot be null.");
    }
}

sealed class FinalizeFileUploadBatchEndpoint(IUploadBatchService uploadBatches)
    : Endpoint<FinalizeFileUploadBatchRequest, FinalizeFileUploadBatchResponse>
{
    // Item rules run here rather than in the request validator, which would reject the whole batch.
    private static readonly FinalizeFileUploadRequestValidator ItemValidator = new();

    public override void Configure()
    {
        Post("files/finalize-upload/batch");
        Policies(Common.Auth.Policies.CanUpload);
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Finalize several uploads";
            s.Description = "Verifies and saves each upload independently, so one bad file doesn't "
                            + "affect the others. An upload that already finished counts as a success, "
                            + "which makes a repeated request safe. Results are in the same order as "
                            + "the items.";
            s.Responses[200] = "Batch processed, see per-item results";
            s.Responses[400] = "Bad request - no items, too many items, or malformed JSON";
        });
    }

    public override async Task HandleAsync(FinalizeFileUploadBatchRequest req, CancellationToken ct)
    {
        var results = new FinalizeFileUploadBatchResult[req.Items.Count];
        var validIndexes = new List<int>(req.Items.Count);

        for (var i = 0; i < req.Items.Count; i++)
        {
            var validation = ItemValidator.Validate(req.Items[i]);

            if (validation.IsValid)
            {
                validIndexes.Add(i);
                continue;
            }

            results[i] = new FinalizeFileUploadBatchResult
            {
                UploadId = req.Items[i].UploadId,
                Success = false,
                Error = validation.Errors[0].ErrorMessage
            };
        }

        if (validIndexes.Count > 0)
        {
            var finalized = await uploadBatches.FinalizeAsync(
                [.. validIndexes.Select(i => ToItem(req.Items[i]))],
                User.GetUserId(),
                ct);

            for (var i = 0; i < validIndexes.Count; i++)
            {
                results[validIndexes[i]] = new FinalizeFileUploadBatchResult
                {
                    UploadId = finalized[i].UploadId,
                    Success = finalized[i].Success,
                    Error = finalized[i].Error
                };
            }
        }

        await Send.OkAsync(new FinalizeFileUploadBatchResponse { Results = results.ToList() }, ct);
    }

    private static UploadFinalizeItem ToItem(FinalizeFileUploadRequest item) => new()
    {
        UploadId = item.UploadId,
        DirectoryId = item.DirectoryId,
        FileName = item.FileName,
        IsEncrypted = item.IsEncrypted,
        EncryptionIv = item.EncryptionIv,
        EncryptionSalt = item.EncryptionSalt,
        IntegrityTag = item.IntegrityTag,
        EncryptionHint = item.EncryptionHint,
        IterationCount = item.IterationCount
    };
}
