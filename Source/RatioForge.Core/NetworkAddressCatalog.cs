namespace RatioForge;

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>Discovers usable local IPv4 and IPv6 addresses.</summary>
public static class NetworkAddressCatalog
{
    /// <summary>Returns active unicast addresses, ordered by address family.</summary>
    public static IReadOnlyList<string> GetLocalAddresses()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address)
            .Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            .Where(address => !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any))
            .OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .ThenBy(address => address.ToString(), StringComparer.Ordinal)
            .Select(address => address.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!addresses.Contains(IPAddress.Loopback.ToString(), StringComparer.Ordinal))
        {
            addresses.Add(IPAddress.Loopback.ToString());
        }

        if (Socket.OSSupportsIPv6 && !addresses.Contains(IPAddress.IPv6Loopback.ToString(), StringComparer.Ordinal))
        {
            addresses.Add(IPAddress.IPv6Loopback.ToString());
        }

        return addresses;
    }

    internal static IPAddress? ParseOptional(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        if (!IPAddress.TryParse(address.Trim(), out IPAddress? parsed))
        {
            throw new FormatException($"'{address}' is not a valid IPv4 or IPv6 address.");
        }

        return parsed;
    }
}
