using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

public sealed record AuditActivityRow(OperationType OperationType, DateTimeOffset Timestamp);
