using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Common.Helpers;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.GetDocumentMetadata;

internal sealed class GetDocumentMetadataRequest
{
    public Guid FileId { get; set; }

    /// <summary>
    /// The version to read. Omit for the file's current version.
    /// </summary>
    [QueryParam]
    public Guid? VersionId { get; set; }
}

/// <summary>
/// A version's document properties, including the names it carries, for the file's owner only.
/// </summary>
internal sealed class GetDocumentMetadataEndpoint(IUnitOfWork unitOfWork)
    : Endpoint<GetDocumentMetadataRequest, DocumentMetadataResponse>
{
    public override void Configure()
    {
        Get("/files/{fileId}/document-metadata");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetDocumentMetadataRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var file = await unitOfWork.Files.GetByIdAsync(req.FileId, ct);
        if (file is null || file.DeletedAt is not null || file.OwnerId != userId)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var versionId = req.VersionId ?? file.CurrentVersionId;
        if (versionId is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var versionExists = await unitOfWork.FileVersions.ExistsAsync(
            v => v.Id == versionId && v.FileId == file.Id && v.DeletedAt == null, ct);
        if (!versionExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var metadata = await unitOfWork.DocumentMetadata.GetByVersionIdAsync(versionId.Value, ct);

        // A book names its author on the cover, so sharing it reveals nothing the author kept
        // private. Office files record account names their owners never chose to publish.
        var isBook = DocumentMetadataFileTypes.IsBook(file.MimeType);

        var response = new DocumentMetadataResponse
        {
            FileVersionId = versionId.Value,
            IsExtracted = metadata is not null,
            HasPersonalDetails = (metadata?.Author is not null && !isBook)
                                 || metadata?.LastModifiedBy is not null
                                 || metadata?.Company is not null,
            IsPasswordProtected = metadata?.IsPasswordProtected ?? false,
            IsDrmProtected = metadata?.IsDrmProtected ?? false,
            Title = metadata?.Title,
            Subject = metadata?.Subject,
            Keywords = metadata?.Keywords,
            Language = metadata?.Language,
            Author = metadata?.Author,
            LastModifiedBy = metadata?.LastModifiedBy,
            Company = metadata?.Company,
            Application = metadata?.Application,
            Producer = metadata?.Producer,
            AuthoredAt = metadata?.AuthoredAt,
            AuthoredOffsetMinutes = metadata?.AuthoredOffsetMinutes,
            ModifiedAt = metadata?.ModifiedAt,
            ModifiedOffsetMinutes = metadata?.ModifiedOffsetMinutes,
            PageCount = metadata?.PageCount,
            WordCount = metadata?.WordCount,
            SlideCount = metadata?.SlideCount,
            SheetCount = metadata?.SheetCount,
            EditingMinutes = metadata?.EditingMinutes,
            PaperSize = metadata?.PaperSize,
            Publisher = metadata?.Publisher,
            Isbn = metadata?.Isbn,
            Series = metadata?.Series,
            SeriesIndex = metadata?.SeriesIndex,
        };

        await Send.OkAsync(response, ct);
    }
}
