using System.Text;
using Common;
using Common.Config;
using Common.Services;
using DTO.Files;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Models.Enumerators;

namespace PreviewService;

public class PreviewService(
    IStorageService storageService,
    IImagePreviewService imagePreviewService,
    IOptions<S3Config> storageConfig,
    IMemoryCache memoryCache,
    IPublisherService publisherService,
    IUnitOfWork unitOfWork,
    ITextPreviewService textPreviewService,
    IFileService fileService,
    IArchivePreviewService archivePreviewService) : IPreviewService
{
    private static readonly SemaphoreSlim Pool = new(3, 3);

    private static readonly Dictionary<Guid, Task<PreviewResultDto>> OngoingTasks = new();
    private static readonly SemaphoreSlim TaskDictionaryLock = new(1, 1);

    public async Task<PreviewResultDto?> GetPreviewUrl(Guid fileId, Guid ownerId, CancellationToken ct)
    {
        var fileData = await unitOfWork.Files.GetByIdAsync(fileId, ct);
        if (fileData is null || fileData.OwnerId != ownerId)
            throw new InvalidOperationException("No file found for preview generation");

        if (memoryCache.TryGetValue(fileId, out PreviewResultDto? cachedValue))
            if (cachedValue is not null)
                return cachedValue;

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
            SlidingExpiration = TimeSpan.FromMinutes(5)
        };
        if (cachedPreview is { PreviewUrl: not null })
        {
            memoryCache.Set(fileId, cachedPreview.PreviewUrl, cacheOptions);
            return cachedPreview;
        }

        await TaskDictionaryLock.WaitAsync(ct);
        Task<string> ongoingTask;

        if (OngoingTasks.TryGetValue(fileId, out var existingTask))
        {
            // Another request is already processing this file so we are going to wait for that one 
            TaskDictionaryLock.Release();
            var result = await existingTask;
            return result;
        }

        var tcs = new TaskCompletionSource<PreviewResultDto>();
        // Create a new task for this file
        OngoingTasks[fileId] = tcs.Task;
        TaskDictionaryLock.Release();

        try
        {
            await Pool.WaitAsync(ct);
            try
            {
                var fileStream = await storageService.DownloadFile(fileId, fileData.OwnerId, ct);
                var category = storageService.CategorizeFile(fileData.MimeType);

                switch (category)
                {
                    case FileCategory.Image:
                        //TODO: Inspect for memory leaks here or excessive downloading from the backend
                        //TODO: Ideally this whole code block should be moved to one of the workers
                        var fileHash = Convert.ToHexStringLower(
                            await unitOfWork.Files.GetFileHash(fileData.Id, fileData.OwnerId, ct)
                            ?? throw new InvalidOperationException("File does not have related content object"));
                        var previewStream =
                            await imagePreviewService.GenerateImagePreview(fileStream, fileData.MimeType);
                        await storageService.UploadPreview(
                            storageConfig.Value.PreviewBucket ?? "user-previews",
                            $"previews/{fileHash}",
                            fileData.MimeType,
                            previewStream,
                            /*
                               TODO: This should be changed to the default system profile once project initialization
                               is seeding the database properly
                            */
                            uploadedBy: Guid.Empty,
                            originalFileId: fileData.Id,
                            ct: ct);
                        await fileService.UpdateFileMetadata(fileId, hasPreview: true, updatedBy: Guid.Empty,
                            ct: ct);

                        var previewResult = await storageService.GetCachedPreview(fileId, ct);
                        if (previewResult is null or { PreviewUrl: null }) throw new InvalidOperationException();

                        memoryCache.Set(fileId, previewResult, cacheOptions);

                        tcs.SetResult(previewResult);

                        return previewResult;
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
                        // var archiveResultTuple = (archivePreview.data, archivePreviewSummary);
                        memoryCache.Set(fileId, archivePreviewResult, cacheOptions);

                        tcs.SetResult(archivePreviewResult);

                        return archivePreviewResult;
                    }

                    case FileCategory.Text:
                        var textPreview =
                            await textPreviewService.GenerateTextPreviewAsync(fileStream, fileData.Name, 524288, ct);

                        var textPreviewSummary =
                            new FileSummary(fileData.Id, fileData.Name, textPreview.mimeType, true);

                        var textPreviewResult = new PreviewResultDto(textPreviewSummary, null, null, textPreview.data);

                        // memoryCache.Set(fileId, textPreviewResult, cacheOptions);

                        tcs.SetResult(textPreviewResult);

                        return textPreviewResult;

                    case FileCategory.Audio:
                    case FileCategory.Video:
                        var mediaBody = Encoding.UTF8.GetBytes(fileId.ToString());
                        await publisherService.Publish(mediaBody, $"media.{fileData.MimeType.Split('/')[1]}");
                        return null;
                    case FileCategory.Unknown:
                    default:
                        var unknownFileSummary = new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, false);
                        var noPreviewResult = new PreviewResultDto(unknownFileSummary, null, null, null);

                        tcs.SetResult(noPreviewResult);

                        return noPreviewResult;
                }
            }
            finally
            {
                Pool.Release();
            }
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
            throw;
        }
        finally
        {
            await TaskDictionaryLock.WaitAsync(ct);
            OngoingTasks.Remove(fileId);
            TaskDictionaryLock.Release();
        }
    }
}