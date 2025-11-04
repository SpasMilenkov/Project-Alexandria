using Common.Config;
using Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace API.Features.Storage.UploadFile;

public class UploadFileEndpoint(IOptions<S3Config> options, IStorageService storage)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/files/upload");
        AllowAnonymous();
        AllowFileUploads();

        // Swagger documentation
        Summary(s =>
        {
            s.Summary = "Upload a file to MinIO storage";
            s.Description = "Uploads a file with optional metadata. Supports large files via streaming.";
            s.Responses[200] = "File uploaded successfully";
            s.Responses[400] = "Bad request - missing file or validation error";
            s.Responses[500] = "Internal server error";
        });

        // Configuration for file upload in Swagger UI
        Options(x =>
        {
            x.WithMetadata(new
            {
                file = "Select file...",
                filename = "optional-custom-name.pdf",
                path = "optional/folder/path",
                expectedchecksum = "optional-sha256-hash"
            });
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        //TODO: add proper user data extraction after introducing JWT authorization and authentication
        var user = "John Doe";
        var bucketName = options.Value.UploadBucket;
        Console.WriteLine($"Bucket: {bucketName}");

        // Ensure bucket exists and versioning is enabled
        if (bucketName is null) ThrowError("Invalid bucket configuration");
        await storage.EnsureBucketExistsAsync(bucketName, ct);

        string? fileName = null;
        string? path = null;
        string contentType = "application/octet-stream";
        Stream? fileStream = null;

        // Parse the multipart form to extract metadata and file stream
        await foreach (var section in FormMultipartSectionsAsync(ct))
        {
            if (section.IsFormSection)
            {
                var fieldName = section.FormSection.Name.ToLowerInvariant();
                var fieldValue = await section.FormSection.GetValueAsync(ct);

                switch (fieldName)
                {
                    case "filename":
                        fileName = fieldValue;
                        break;
                    case "path":
                        path = fieldValue;
                        break;
                }
            }
            else if (section.IsFileSection)
            {
                // Get file metadata
                fileName ??= section.FileSection.FileName;
                contentType = section.FileSection.Section.ContentType ?? contentType;
                // FileLength is not available, we'll use -1 for unknown size
                fileStream = section.FileSection.FileStream;

                // Process the file stream immediately
                break;
            }
        }

        if (fileStream == null)
        {
            ThrowError("No file found in the request.");
            return;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            ThrowError("File name is required.");
            return;
        }
        
        // if (contentType == "application/octet-stream")
        // {
        //     var provider = new FileExtensionContentTypeProvider();
        //     if (provider.TryGetContentType(fileName, out var inferred))
        //         contentType = inferred;
        // }
        
        // Construct object name
        var objectName = string.IsNullOrWhiteSpace(path)
            ? fileName
            : $"{path.TrimEnd('/')}/{fileName}";

        try
        {
            // Upload to MinIO using the checksum stream
            var uploadResult = await storage.UploadFile(bucketName: bucketName, objectName, contentType, fileStream, ct,
                -1, fileName, user);

            // Get the calculated checksum

            var stat = await storage.GetVersionInfo(bucketName, objectName, ct);
            await Send.OkAsync(new UploadFileResponse
            {
                ObjectName = uploadResult.ObjectName,
                Url = uploadResult.Url,
                Checksum = uploadResult.Checksum,
                VersionId = uploadResult.VersionId,
                Size = uploadResult.Size,
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Upload failed: {ex.Message}");
            ThrowError($"Upload failed: {ex.Message}");
        }
    }
}