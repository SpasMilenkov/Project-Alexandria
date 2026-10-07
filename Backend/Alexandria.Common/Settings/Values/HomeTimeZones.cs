using System.Collections.Frozen;

namespace Alexandria.Common.Settings.Values;

public static class HomeTimeZones
{
    public const string LocalId = "local";
    public const int MaxIdentifierLength = 128;

    public static IReadOnlyList<string> Identifiers { get; } = Array.AsReadOnly(CreateIdentifiers());

    private static readonly FrozenSet<string> IdentifierSet = Identifiers.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsSupported(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > MaxIdentifierLength)
            return false;

        if (IdentifierSet.Contains(identifier))
            return true;

        // ICU recognizes aliases omitted by the system's primary-zone catalogue.
        return TimeZoneInfo.TryConvertIanaIdToWindowsId(identifier, out _) &&
               TimeZoneInfo.TryFindSystemTimeZoneById(identifier, out var zone) && zone.HasIanaId;
    }

    private static string[] CreateIdentifiers()
    {
        var identifiers = TimeZoneInfo.GetSystemTimeZones()
            .Select(ToIanaIdentifier)
            .OfType<string>()
            .Where(identifier => identifier != "UTC")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        return [LocalId, "UTC", .. identifiers];
    }

    private static string? ToIanaIdentifier(TimeZoneInfo zone)
    {
        if (zone.HasIanaId)
            return zone.Id;

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var identifier) &&
            TimeZoneInfo.TryFindSystemTimeZoneById(identifier, out _))
            return identifier;

        return null;
    }
}