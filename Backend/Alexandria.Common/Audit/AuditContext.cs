using Alexandria.Common.Config;

namespace Alexandria.Common.Audit;

public class AuditContext
{
    public Guid? UserId { get; private set; }

    public void RunAsSystem()
    {
        UserId = SystemConfig.SystemId;
    }

    public void Clear()
    {
        UserId = null;
    }
}