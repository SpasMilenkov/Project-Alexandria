namespace Alexandria.Data.Models;

public class FileEnrichment : IBase
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public File File { get; set; } = null!;

    public required string Analyzer { get; set; } // e.g. "essentia-genre", "essentia-mood"
    public required string Version { get; set; } // model/pipeline version, e.g. "MAEST-519l"
    public required string PayloadJson { get; set; } // raw worker output, mapped to jsonb

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}