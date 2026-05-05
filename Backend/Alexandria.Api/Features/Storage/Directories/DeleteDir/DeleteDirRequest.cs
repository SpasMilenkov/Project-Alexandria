namespace Alexandria.Api.Features.Storage.Directories.DeleteDir;

public class DeleteDirRequest
{
    public Guid Id { get; set; }
    public bool Force { get; set; }
}