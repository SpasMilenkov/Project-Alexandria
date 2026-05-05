using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.FinalizeFileUpload;

internal class FinalizeFileUploadRequestValidator : Validator<FinalizeFileUploadRequest>
{
    public FinalizeFileUploadRequestValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty().WithMessage("Upload ID cannot be empty");
        RuleFor(x => x.FileName).NotEmpty().WithMessage("File name cannot be empty");

        RuleFor(x => x.EncryptionIv)
            .NotEmpty().NotNull()
            .Must(iv => iv?.Length == 12)
            .WithMessage("Encryption IV must be 12 bytes")
            .When(x => x.IsEncrypted);

        RuleFor(x => x.EncryptionSalt)
            .NotEmpty().NotNull()
            .Must(salt => salt?.Length == 16)
            .WithMessage("Encryption salt must be 16 bytes")
            .When(x => x.IsEncrypted);

        RuleFor(x => x.IntegrityTag)
            .NotEmpty().NotNull()
            .Must(tag => tag?.Length == 16)
            .WithMessage("Integrity tag must be 16 bytes")
            .When(x => x.IsEncrypted);

        RuleFor(x => x.EncryptionHint)
            .MaximumLength(ValidationConstants.StringLengths.ShortString)
            .When(x => x.IsEncrypted);

        RuleFor(x => x.IterationCount)
            .NotEmpty().NotNull()
            .Must(count => count >= 800_000)
            .When(x => x.IsEncrypted);
    }
}