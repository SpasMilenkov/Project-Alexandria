using Models.Enumerators;

namespace Models;

public class AuditLog
{
     public Guid Id { get; set; }
     public required OperationType OperationType { get; set; }
     
     public Guid? UserId { get; set; }
 
     public required EntityType EntityType { get; set; } // "File", "Directory", "User"
     public Guid? EntityId { get; set; }
 
     public required string Description { get; set; } 
     // "File 'Report.pdf' renamed to 'Report_v2.pdf'"
 
     public string? MetadataJson { get; set; } 
     // Optional structured data (before/after names, IP, etc.)
 
     public DateTimeOffset Timestamp { get; set; }
 
     public string? IpAddress { get; set; }
     
     public LogSource Source { get; set; }  // API or Trigger
    
     // NEW: For trigger-created logs pending enrichment
     public bool IsEnriched { get; set; }

 }