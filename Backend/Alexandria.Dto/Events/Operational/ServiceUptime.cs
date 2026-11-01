using Alexandria.Data.Models.Enumerators.Monitoring;

namespace Alexandria.Dto.Events.Operational;

public sealed record ServiceUptime(ServiceType Service, double Uptime);
