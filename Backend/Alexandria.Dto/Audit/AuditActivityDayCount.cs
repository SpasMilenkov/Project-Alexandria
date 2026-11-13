using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Audit;

/// <summary>How many times one operation happened on one UTC day.</summary>
public sealed record AuditActivityDayCount(DateOnly Day, OperationType OperationType, int Count);
