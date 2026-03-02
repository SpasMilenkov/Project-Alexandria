namespace Models;

public class AdminSettings : IBase
{
    public Guid Id { get; set; }

    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
