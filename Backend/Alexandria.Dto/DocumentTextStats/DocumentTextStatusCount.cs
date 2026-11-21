using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.DocumentTextStats;

/// <summary>How many documents' text ended in one outcome.</summary>
public sealed record DocumentTextStatusCount(DocumentTextStatus Status, int Count);