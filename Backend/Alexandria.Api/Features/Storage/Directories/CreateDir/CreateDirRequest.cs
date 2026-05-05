namespace Alexandria.Api.Features.Storage.Directories.CreateDir;

public class CreateDirRequest
{
    public required string Name { get; set; }
    public required Guid? ParentId { get; set; }
}