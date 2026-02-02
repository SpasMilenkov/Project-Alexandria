namespace DTO.Tags;

public class TagSearchQuery
{
    public Guid? UserId { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? UpdatedAfter { get; set; }
    public DateTime? UpdatedBefore { get; set; }
    public Guid? ExcludeOnFile { get; set; }
    public string? NameContains { get; set; }
    public bool? HasFiles { get; set; }
    public int CurrentPage { get; set; } = 0;
    public int PageSize { get; set; } = 20;
}