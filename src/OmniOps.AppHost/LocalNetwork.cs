using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>This computer's address on the local network, for testing on a phone over Wi-Fi.</summary>
internal static class LocalNetwork
{
    /// <summary>
    /// The IPv4 address of the interface that has a default gateway (the Wi-Fi or Ethernet connection), or null.
    /// Docker and VPN bridges have no gateway, so they are skipped.
    /// </summary>
    public static string? Address() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up
            && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
        .Select(n => n.GetIPProperties())
        .Where(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
        .SelectMany(p => p.UnicastAddresses)
        .Select(a => a.Address)
        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a))?
        .ToString();

    private static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
