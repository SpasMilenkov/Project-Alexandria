namespace Models;

public class Directory : IBase
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid? ParentId { get; set; }
    public Directory? Parent { get; set; }
    public List<Directory>? Children { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public List<File>? Files { get; set; }
    public ApplicationUser? Owner { get; set; }
    public Guid OwnerId { get; set; }
}