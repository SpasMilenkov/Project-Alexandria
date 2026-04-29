namespace Alexandria.Common.Config;

public static class SystemConfig
{
    public static string SystemAccountName { get; set; } = "System";
    public static string SystemEmail { get; set; } = "system_account@alexandria.internal";
    public static Guid SystemId { get; } = new Guid("00000000-0000-0000-0000-000000000001");
}