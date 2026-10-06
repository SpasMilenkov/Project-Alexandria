using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Builder.Models;

public sealed class PublicEndpoint
{
    public string Host { get; set; } = string.Empty;
    public int HttpsPort { get; set; } = 443;

    [JsonIgnore]
    public string Origin
    {
        get
        {
            Validate();

            return HttpsPort == 443 ? $"https://{Host}" : $"https://{Host}:{HttpsPort}";
        }
    }

    public void Validate()
    {
        if (!TryNormalizeHost(Host, out var normalized, out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (HttpsPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("Choose an HTTPS port between 1 and 65535.");
        }

        Host = normalized;
    }

    public static bool TryNormalizeHost(string input, out string host, out string? error)
    {
        host = input.Trim().TrimEnd('.').ToLowerInvariant();
        error = null;

        if (string.IsNullOrEmpty(host) || host.Any(char.IsWhiteSpace) || host.IndexOfAny(['/', '\\', ':', '@', '*', '?', '#', '%', '[', ']']) >= 0)
        {
            error = "Enter a LAN IPv4 address or DNS name, without a scheme, path, or port. IPv6 is not supported yet.";

            return false;
        }

        if (host.All(character => char.IsAsciiDigit(character) || character == '.'))
        {
            if (!Regex.IsMatch(host, @"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$") || !IPAddress.TryParse(host, out var address) ||
                address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address) ||
                address.GetAddressBytes()[0] is 0 or >= 224 || address.ToString() != host)
            {
                error = "Enter a complete LAN IPv4 address; loopback, wildcard, multicast, and abbreviated addresses cannot serve other devices.";

                return false;
            }

            return true;
        }

        try
        {
            host = new IdnMapping().GetAscii(host).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            error = "Enter a valid DNS name that your LAN devices can resolve.";

            return false;
        }

        if (host is "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) || host.Length > 253 ||
            host.Split('.').Any(label => label.Length is 0 or > 63 || !Regex.IsMatch(label, "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")))
        {
            error = "Enter a LAN DNS name that resolves on your devices; localhost and invalid DNS labels are unsupported.";

            return false;
        }

        return true;
    }

    public static string SuggestHost()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Select(address => address.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .FirstOrDefault(value => TryNormalizeHost(value, out _, out _)) ?? string.Empty;
        }
        catch (NetworkInformationException)
        {
            return string.Empty;
        }
    }
}
