using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Dto.Events.Operational;

/// <summary>
/// The lifetime of one service-failure incident; <see cref="End"/> is null while it is still open.
/// </summary>
public sealed record DowntimeInterval(ServiceType ServiceType, DateTime Start, DateTime? End);
