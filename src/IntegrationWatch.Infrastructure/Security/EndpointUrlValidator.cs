using System.Net;
using System.Net.Sockets;
using IntegrationWatch.Core.Interfaces;

namespace IntegrationWatch.Infrastructure.Security;

public class EndpointUrlValidator : IEndpointUrlValidator
{
    public UrlValidationResult ValidateUrl(string url, bool allowPrivateNetworks = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return UrlValidationResult.Failure("Target URL cannot be empty.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return UrlValidationResult.Failure("Malformed URL format.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return UrlValidationResult.Failure($"Unsupported URI scheme '{uri.Scheme}'. Only HTTP and HTTPS are permitted.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return UrlValidationResult.Failure("Target host is empty.");
        }

        if (allowPrivateNetworks)
        {
            return UrlValidationResult.Success();
        }

        // Host-level checks
        var host = uri.Host.Trim().ToLowerInvariant();
        if (host == "localhost" || host.EndsWith(".localhost") || host.EndsWith(".local") || host.EndsWith(".internal"))
        {
            return UrlValidationResult.Failure($"Target host '{uri.Host}' resolves to a local or internal domain, which is restricted (SSRF prevention).");
        }

        // IP address resolution & boundary checks
        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var directIp))
        {
            addresses = new[] { directIp };
        }
        else
        {
            try
            {
                addresses = Dns.GetHostAddresses(uri.DnsSafeHost);
            }
            catch (SocketException)
            {
                // Unresolvable hosts will fail at HTTP client execution, but are not SSRF
                return UrlValidationResult.Success();
            }
            catch (Exception ex)
            {
                return UrlValidationResult.Failure($"DNS resolution failed for host '{uri.Host}': {ex.Message}");
            }
        }

        foreach (var ip in addresses)
        {
            if (IsRestrictedAddress(ip))
            {
                return UrlValidationResult.Failure($"Target host '{uri.Host}' resolves to a restricted private or loopback IP address ({ip}) (SSRF prevention).");
            }
        }

        return UrlValidationResult.Success();
    }

    public static bool IsRestrictedAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();

            // 0.0.0.0/8 (Broadcast/This host)
            if (bytes[0] == 0) return true;

            // 10.0.0.0/8 (RFC 1918 Private)
            if (bytes[0] == 10) return true;

            // 100.64.0.0/10 (Carrier-Grade NAT)
            if (bytes[0] == 100 && (bytes[1] >= 64 && bytes[1] <= 127)) return true;

            // 127.0.0.0/8 (Loopback)
            if (bytes[0] == 127) return true;

            // 169.254.0.0/16 (Link-Local & Cloud Metadata 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254) return true;

            // 172.16.0.0/12 (RFC 1918 Private)
            if (bytes[0] == 172 && (bytes[1] >= 16 && bytes[1] <= 31)) return true;

            // 192.168.0.0/16 (RFC 1918 Private)
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            // 224.0.0.0/4 (Multicast)
            if (bytes[0] >= 224 && bytes[0] <= 239) return true;

            // 240.0.0.0/4 (Reserved)
            if (bytes[0] >= 240) return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;

            var bytes = ip.GetAddressBytes();
            // Unique Local Address fc00::/7
            if ((bytes[0] & 0xFE) == 0xFC) return true;
        }

        return false;
    }
}
