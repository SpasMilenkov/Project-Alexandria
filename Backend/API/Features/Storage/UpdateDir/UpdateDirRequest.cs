namespace API.Features.Storage.UpdateDir;

public class UpdateDirRequest
{
    public required string Name { get; set; }
    public Guid DirectoryId { get; set; }
}