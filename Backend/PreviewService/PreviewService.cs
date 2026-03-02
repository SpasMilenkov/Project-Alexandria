using System.Text;
using Common;
using Common.Services;
using DTO.Files;
using Microsoft.Extensions.Caching.Memory;
using Models.Enumerators;

namespace PreviewService;

public class PreviewService(
    IStorageService storageService,
    IMemoryCache memoryCache,
    IPublisherService publisherService,
    IUnitOfWork unitOfWork,
    ITextPreviewService textPreviewService,
    IArchivePreviewService archivePreviewService) : IPreviewService
{

    public async Task<PreviewResultDto?> GetPreviewUrl(Guid fileId, Guid ownerId, CancellationToken ct)
    {
        var fileData = await unitOfWork.Files.GetByIdAsync(fileId, ct);
        if (fileData is null || fileData.OwnerId != ownerId)
            throw new InvalidOperationException("No file found for preview generation");

        if (memoryCache.TryGetValue(fileId, out PreviewResultDto? cachedValue) && cachedValue is not null) return cachedValue;

        if (!await unitOfWork.Files.IsPromoted(fileId, ct))
        {
            var unknownFileSummary = new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, false);
            var noPreviewResult = new PreviewResultDto(unknownFileSummary, null, null, null);

            return noPreviewResult;
        }

        var cachedPreview = await storageService.GetCachedPreview(fileId, ct);
        var cacheOptions = new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = TimeSpan.FromMinutes(2)
        };
        if (cachedPreview is { PreviewUrl: not null })
        {
            memoryCache.Set(fileId, cachedPreview.PreviewUrl, cacheOptions);
            return cachedPreview;
        }

        var tcs = new TaskCompletionSource<PreviewResultDto>();
        try
        {
            var fileStream = await storageService.DownloadFile(fileId, fileData.OwnerId, ct);
            var category = storageService.CategorizeFile(fileData.MimeType);

            switch (category)
            {
                case FileCategory.Image:
                    var imageBody = Encoding.UTF8.GetBytes(fileId.ToString());
                    await publisherService.Publish(imageBody, $"image.{fileData.MimeType.Split('/')[1]}");
                    return null;

                case FileCategory.Document:
                case FileCategory.Spreadsheet:
                case FileCategory.Presentation:
                case FileCategory.Pdf:
                    var documentBody = Encoding.UTF8.GetBytes(fileId.ToString());
                    await publisherService.Publish(documentBody, $"document.{fileData.MimeType.Split('/')[1]}");
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
                        memoryCache.Set(fileId, archivePreviewResult, cacheOptions);

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
                    await publisherService.Publish(mediaBody, $"media.{fileData.MimeType.Split('/')[1]}");
                    return null;
                default:
                    var unknownFileSummary = new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, false);
                    var noPreviewResult = new PreviewResultDto(unknownFileSummary, null, null, null);

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
