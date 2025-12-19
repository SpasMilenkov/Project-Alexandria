using System.Text;
using Common;
using Common.Config;
using Common.Services;
using DTO;
using DTO.Files;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Models.Enumerators;
using PreviewService.Archives;

namespace PreviewService;

public class PreviewService(
    IStorageService storageService,
    IImagePreviewService imagePreviewService,
    IOptions<S3Config> storageConfig,
    IMemoryCache memoryCache,
    IPublisherService publisherService,
    ITextPreviewService textPreviewService,
    IArchivePreviewService archivePreviewService) : IPreviewService
{
    private static readonly SemaphoreSlim Pool = new(3, 3);

    private static readonly Dictionary<Guid, Task<(byte[] data, FileSummary metadata)>> OngoingTasks = new();
    private static readonly SemaphoreSlim TaskDictionaryLock = new(1, 1);

    //TODO: break down this monstrosity into smaller more manageable chunks
    //TODO: Replace all Guid.Empty with the system account ID when that is seeded into the database
    public async Task<FileResultSummary?> GetPreview(Guid fileId, CancellationToken ct)
    {
         if (memoryCache.TryGetValue(fileId, out (byte[] fileBytes, FileSummary metadata) cacheValue))
            return new FileResultSummary(new MemoryStream(cacheValue.fileBytes), cacheValue.metadata);

         var cachedPreview = await storageService.GetCachedPreview(fileId, ct);
         if (cachedPreview is not null)
         {
             using var ms = new MemoryStream();
             await cachedPreview.FileStream.CopyToAsync(ms, ct);
             var previewData = ms.ToArray();

             var cacheOptions = new MemoryCacheEntryOptions
             {
                 Size = Math.Max(1, previewData.Length / (1024 * 1024)),
                 SlidingExpiration = TimeSpan.FromMinutes(5)
             };

             memoryCache.Set(fileId, (previewData, cachedPreview.Metadata), cacheOptions);

             await cachedPreview.FileStream.DisposeAsync();

             return new FileResultSummary(new MemoryStream(previewData), cachedPreview.Metadata);
         }

         await TaskDictionaryLock.WaitAsync(ct);
         Task<(byte[] data, FileSummary metadata)> ongoingTask;

         if (OngoingTasks.TryGetValue(fileId, out var existingTask))
         {
             // Another request is already processing this file so we are going to wait for that one 
             TaskDictionaryLock.Release();
             var result = await existingTask;
             return new FileResultSummary(new MemoryStream(result.data), result.metadata);
         }

         var tcs = new TaskCompletionSource<(byte[] data, FileSummary metadata)>();
         // Create a new task for this file
         OngoingTasks[fileId] = tcs.Task;
         TaskDictionaryLock.Release();

         try
         {
             await Pool.WaitAsync(ct);
             try
             {
                 var fileData = await storageService.GetFileMetadata(fileId, ct);
                 if (fileData is null) throw new FileNotFoundException();
         
                 var fileStream = await storageService.DownloadFile(null, fileData.Name, ct);
                 var category = storageService.CategorizeFile(fileData.MimeType);
         
                 switch (category)
                 {
                     case FileCategory.Image:
                         //TODO: Inspect for memory leaks here or excessive downloading from the backend
                         //TODO: Ideally this whole code block should be moved to one of the workers
                         var previewStream =
                             await imagePreviewService.GenerateImagePreview(fileStream, fileData.MimeType);
                         await storageService.UploadPreview(
                             storageConfig.Value.PreviewBucket ?? "user-previews",
                             "previews/" + fileData.Name,
                             fileData.MimeType,
                             previewStream,
                             /*
                                TODO: This should be changed to the default system profile once project initialization
                                is seeding the database properly 
                             */
                             uploadedBy: Guid.Empty,
                             originalFileId: fileData.Id,
                             ct: ct);
                         await storageService.UpdateFileMetadata(fileId, hasPreview: true, updatedBy: Guid.Empty, ct: ct);
         
                         var previewResult = await storageService.GetCachedPreview(fileId, ct);
                         if (previewResult is null) throw new InvalidOperationException();
         
                         // Read stream into byte array for caching
                         using (var ms = new MemoryStream())
                         {
                             await previewResult.FileStream.CopyToAsync(ms, ct);
                             var previewData = ms.ToArray();
         
                             var cacheOptions = new MemoryCacheEntryOptions
                             {
                                 Size = Math.Max(1, previewData.Length / (1024 * 1024)),
                                 SlidingExpiration = TimeSpan.FromMinutes(5)
                             };
         
                             var resultTuple = (previewData, previewResult.Metadata);
                             memoryCache.Set(fileId, resultTuple, cacheOptions);
         
                             await previewResult.FileStream.DisposeAsync();
         
                             tcs.SetResult(resultTuple);
         
                             return previewResult with { FileStream = new MemoryStream(previewData) };
                         }
                     case FileCategory.Document:
                     case FileCategory.Spreadsheet:
                     case FileCategory.Presentation:
                     case FileCategory.Pdf:
                         var documentBody = Encoding.UTF8.GetBytes(fileId.ToString());
                         await publisherService.Publish(documentBody, $"document.{fileData.MimeType.Split('/')[1]}");
                         return null;
                     case FileCategory.Archive:
                         var archivePreview = await archivePreviewService.GenerateArchivePreviewAsync(fileStream, fileData.Name, ct);
                         
                         var archivePreviewUpload = await storageService.UploadPreview(
                             storageConfig.Value.PreviewBucket ?? "user-previews",
                             "previews/" + fileData.Name + ".json",
                             "application/json", new MemoryStream(archivePreview.data), fileData.Id,uploadedBy:Guid.Empty,
                             ct: ct);
                         
                         await storageService.UpdateFileMetadata(fileId, hasPreview: true, updatedBy: Guid.Empty, ct: ct);
         
                         var archivePreviewSummary = new FileSummary(fileData.Id, fileData.Name + ".json", "application/json", true, archivePreviewUpload.Url);
         
                         var archiveCacheOptions = new MemoryCacheEntryOptions
                         {
                             Size = Math.Max(1, archivePreview.data.Length / (1024 * 1024)),
                             SlidingExpiration = TimeSpan.FromMinutes(5)
                         };
         
                         var archiveResultTuple = (archivePreview.data, archivePreviewSummary);
                         memoryCache.Set(fileId, archiveResultTuple, archiveCacheOptions);
                             
                         tcs.SetResult(archiveResultTuple);
         
                         return new FileResultSummary(new MemoryStream(archivePreview.data),
                             archivePreviewSummary);
                     
                     case FileCategory.Text:
                         var textPreview = await textPreviewService.GenerateTextPreviewAsync(fileStream, fileData.Name, 524288, ct); 
                         
                         var textPreviewUpload = await storageService.UploadPreview(
                             storageConfig.Value.PreviewBucket ?? "user-previews",
                             "previews/" + fileData.Name,
                             textPreview.mimeType,
                             new MemoryStream(textPreview.data),
                             fileData.Id,
                             uploadedBy: Guid.Empty,
                             ct: ct);
                         
                         await storageService.UpdateFileMetadata(fileId, hasPreview: true, updatedBy: Guid.Empty, ct: ct);
         
                         var textPreviewSummary = new FileSummary(fileData.Id, fileData.Name, "application/json", true, textPreviewUpload.Url);
         
                             var textCacheOptions = new MemoryCacheEntryOptions
                             {
                                 Size = Math.Max(1, textPreview.data.Length / (1024 * 1024)),
                                 SlidingExpiration = TimeSpan.FromMinutes(5)
                             };
         
                             var textResultTuple = (textPreview.data, textPreviewSummary);
                             memoryCache.Set(fileId, textResultTuple, textCacheOptions);
                             
                             tcs.SetResult(textResultTuple);
         
                             return new FileResultSummary(new MemoryStream(textPreview.data),
                                 new FileSummary(fileData.Id, fileData.Name, textPreview.mimeType, true, textPreviewUpload.Url));
         
                     case FileCategory.Audio:
                     case FileCategory.Video:
                         var mediaBody = Encoding.UTF8.GetBytes(fileId.ToString());
                         await publisherService.Publish(mediaBody, $"media.{fileData.MimeType.Split('/')[1]}");
                         return null;
                     case FileCategory.Unknown:
                     default:
                         throw new ArgumentOutOfRangeException();
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

         throw new NotImplementedException();
    }

    public async Task<FileResultSummary?> GetThumbnail(Guid fileId, int width, int height,
        CancellationToken ct = default)
    {
        var cacheKey = $"{fileId}_{width}x{height}";
        if (memoryCache.TryGetValue(cacheKey, out (byte[] fileBytes, FileSummary metadata) cacheValue))
            return new FileResultSummary(new MemoryStream(cacheValue.fileBytes), cacheValue.metadata);

        await TaskDictionaryLock.WaitAsync(ct);

        if (OngoingTasks.TryGetValue(fileId, out var existingTask))
        {
            // Another request is already processing this file so we are going to wait for that one 
            TaskDictionaryLock.Release();
            var result = await existingTask;
            return new FileResultSummary(new MemoryStream(result.data), result.metadata);
        }

        var tcs = new TaskCompletionSource<(byte[] data, FileSummary metadata)>();
        OngoingTasks[fileId] = tcs.Task;
        TaskDictionaryLock.Release();

        try
        {
            await Pool.WaitAsync(ct);
            try
            {
                var fileData = await storageService.GetFileMetadata(fileId, ct);
                if (fileData is null) throw new FileNotFoundException();

                var fileStream = await storageService.DownloadFile(null, fileData.Name, ct);

                var previewStream = await imagePreviewService.GenerateImagePreview(fileStream, fileData.MimeType, width: width, height: height);

                if (previewStream is null) throw new InvalidOperationException();

                using var ms = new MemoryStream();
                await previewStream.CopyToAsync(ms, ct);
                var previewData = ms.ToArray();

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    Size = Math.Max(1, previewData.Length / (1024 * 1024)),
                    SlidingExpiration = TimeSpan.FromMinutes(20)
                };

                var metadataSummary = new FileSummary(fileData.Id, fileData.Name, fileData.MimeType, fileData.HasPreview, "");

                var resultTuple = (previewData, metadataSummary);
                memoryCache.Set(cacheKey, resultTuple, cacheOptions);

                tcs.SetResult(resultTuple);

                return new FileResultSummary(new MemoryStream(previewData), metadataSummary);
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