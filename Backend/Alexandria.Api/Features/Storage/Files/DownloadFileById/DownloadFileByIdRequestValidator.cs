using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.DownloadFileById;

public class DownloadFileByIdRequestValidator : AbstractValidator<DownloadFileByIdRequest>
{
    public DownloadFileByIdRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("File ID cannot be empty");
    }
}