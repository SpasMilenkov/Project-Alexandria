using System.Text;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;

namespace Alexandria.Services.Preview;

public class PreviewService(
    IStorageService storageService,
    IPublisherService publisherService,
    IUnitOfWork unitOfWork,
    ITextPreviewService textPreviewService,
    IArchivePreviewService archivePreviewService) : IPreviewService
{
    /// <inheritdoc/>
    public async Task<bool> HasPreviewAsync(Guid versionId, CancellationToken ct = default)
    {
        return await unitOfWork.Files.ExistsAsync(f => f.Versions
            .Select(v => v.Id)
            .Contains(versionId) && !f.HasPreview, ct);
    }

    /// <inheritdoc/>
    public async Task<PreviewResultDto?> GetPreviewUrlAsync(Guid fileId, Guid ownerId, CancellationToken ct = default)
    {
        var fileData = await unitOfWork.Files.GetByIdAsync(fileId, ct);
        if (fileData is null || fileData.OwnerId != ownerId)
            throw new InvalidOperationException("No file found for preview generation");

        if (await unitOfWork.FileVersions.IsEncryptedAsync(
                fileData.CurrentVersionId ?? throw new InvalidOperationException(""), ct)) return null;

        if (!await unitOfWork.Files.IsPromotedAsync(fileId, ct))
        {
            var unknownFileSummary = new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, false);
            var noPreviewResult = new PreviewResultDto(unknownFileSummary, null, null);

            return noPreviewResult;
        }

        var cachedPreview = await storageService.GetCachedPreview(fileId, ct);

        if (cachedPreview is { PreviewUrl: not null })
            return cachedPreview;

        var tcs = new TaskCompletionSource<PreviewResultDto>();
        try
        {
            var fileStream = await storageService.DownloadFile(fileId, fileData.OwnerId, ct);
            var category = storageService.CategorizeFile(fileData.MimeType);

            switch (category)
            {
                case FileCategory.Image:
                    var imageBody = Encoding.UTF8.GetBytes(fileId.ToString());
                    await publisherService.PublishAsync(imageBody, $"image.{fileData.MimeType.Split('/')[1]}");
                    return null;

                case FileCategory.Document:
                case FileCategory.Spreadsheet:
                case FileCategory.Presentation:
                case FileCategory.Pdf:
                    var documentBody = Encoding.UTF8.GetBytes(fileId.ToString());
                    await publisherService.PublishAsync(documentBody, $"document.{fileData.MimeType.Split('/')[1]}");
                    return null;
                case FileCategory.Archive:
                {
                    await using var seekableArchiveStream =
                        await storageService.DownloadStreamableFile(fileId, fileData.OwnerId, ct);

                    var archivePreview =
                        await archivePreviewService.GenerateArchivePreviewAsync(seekableArchiveStream,
                            fileData.Name, ct);

                    var archivePreviewSummary = new FileSummary(fileData.Id, fileData.Name + ".json",
                        "application/json", true);

                    var archivePreviewResult = new PreviewResultDto(archivePreviewSummary, null, null, null,
                        archivePreview.data);

                    tcs.SetResult(archivePreviewResult);

                    return archivePreviewResult;
                }

                case FileCategory.Text:
                    var (data, mimeType) =
                        await textPreviewService.GenerateTextPreviewAsync(fileStream, fileData.Name, 524288, ct);

                    var textPreviewSummary =
                        new FileSummary(fileData.Id, fileData.Name, mimeType, true);

                    var textPreviewResult = new PreviewResultDto(textPreviewSummary, null, null, data);

                    tcs.SetResult(textPreviewResult);

                    return textPreviewResult;

                case FileCategory.Audio:
                case FileCategory.Video:
                    var mediaBody = Encoding.UTF8.GetBytes(fileId.ToString());
                    await publisherService.PublishAsync(mediaBody, $"media.{fileData.MimeType.Split('/')[1]}");
                    return null;
                default:
                    var unknownFileSummary = new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, false);
                    var noPreviewResult = new PreviewResultDto(unknownFileSummary, null, null);

                    tcs.SetResult(noPreviewResult);

                    return noPreviewResult;
            }
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
            throw;
        }
    }
}