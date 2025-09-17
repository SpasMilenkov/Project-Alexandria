using FastEndpoints;
using Infrastructure.Domain.Services;

namespace API.Features.UpdateFileMetadata;

public class UpdateFileMetadataEndpoint(
    IStorageService storageService
) : Endpoint<UpdateFileMetadataRequest, UpdateFileMetadataResponse>
{
    public override void Configure()
    {
        Put("/file/{id}/metadata");
        AllowAnonymous(); // You might want to add proper authorization here

        Summary(s =>
        {
            s.Summary = "Update file metadata";
            s.Description = "Updates file metadata such as name and preview status.";
            s.Responses[200] = "File metadata updated successfully";
            s.Responses[404] = "File not found";
            s.Responses[400] = "Bad request - validation error";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(UpdateFileMetadataRequest req, CancellationToken ct)
    {
        try
        {
            //TODO: add proper user data extraction after introducing JWT authorization and authentication
            var user = "John Doe";

            var updatedFile = await storageService.UpdateFileMetadata(
                req.Id,
                req.Name,
                req.HasPreview,
                user,
                ct);

            await Send.OkAsync(new UpdateFileMetadataResponse
            {
                Id = updatedFile.Id,
                Name = updatedFile.Name,
                HasPreview = updatedFile.HasPreview,
                PreviewGeneratedAt = updatedFile.PreviewGeneratedAt,
                UpdatedAt = updatedFile.UpdatedAt,
                UpdatedBy = updatedFile.UpdatedBy
            }, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await Send.NotFoundAsync(ct);
        }
        catch (Exception ex)
        {
            ThrowError($"Update failed: {ex.Message}");
        }
    }
}

public class UpdateFileMetadataRequest
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public bool? HasPreview { get; set; }
}

public class UpdateFileMetadataResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool HasPreview { get; set; }
    public DateTime? PreviewGeneratedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}