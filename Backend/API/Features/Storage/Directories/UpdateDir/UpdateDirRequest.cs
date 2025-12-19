namespace API.Features.Storage.Directories.UpdateDir;

public class UpdateDirRequest
{
    public required string Name { get; set; }
    public Guid DirectoryId { get; set; }
}