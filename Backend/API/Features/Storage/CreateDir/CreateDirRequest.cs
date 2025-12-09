using FastEndpoints;

namespace API.Features.Storage.CreateDir;

public class CreateDirRequest
{
    public required string Name { get; set; }
    public required Guid? ParentId { get; set; }
}