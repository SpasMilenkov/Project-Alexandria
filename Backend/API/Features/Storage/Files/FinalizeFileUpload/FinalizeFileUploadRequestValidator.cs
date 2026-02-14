using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Files.FinalizeFileUpload;

internal class FinalizeFileUploadRequestValidator : Validator<FinalizeFileUploadRequest>
{
    public FinalizeFileUploadRequestValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty().WithMessage("Upload ID cannot be empty");
        RuleFor(x => x.FileName).NotEmpty().WithMessage("File name cannot be empty");
    }
}