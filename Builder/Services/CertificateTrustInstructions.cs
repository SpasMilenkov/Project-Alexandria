using Builder.Models;

namespace Builder.Services;

public static class CertificateTrustInstructions
{
    public static string Render(InstallationConfig config, CertificateResult certificate)
    {
        var trust = certificate.IsGenerated
            ? "Trust only the public local-ca.crt on each client. Keep local-ca.key and server.key private."
            : "Use your certificate issuer's trust instructions. For an explicitly self-signed certificate, trust only the public server.crt on each client; some Android browsers require a local CA instead.";

        return $"""
            Alexandria HTTPS access

            Address: {config.PublicEndpoint.Origin}
            Server certificate SHA-256: {certificate.CertificateSha256Fingerprint}
            Certificate expires (UTC): {certificate.ExpiresAt:yyyy-MM-dd HH:mm:ss}
            Public trust file: {certificate.TrustCertificatePath}
            Trust certificate SHA-256: {certificate.TrustSha256Fingerprint}

            {trust}
            Verify the fingerprint above before trusting a file transferred to another device.
            No operating system or browser trust store has been changed by Builder.

            Android: copy the public trust certificate to the device and use Settings > Security
            > Encryption & credentials > Install a certificate > CA certificate (menu names vary).
            Desktop: import the public CA into the trusted certificate authorities of the OS or
            browser you use. Firefox may have its own Authorities store.
            Open the exact HTTPS address above. Do not bypass a browser certificate warning.
            Your DNS name must resolve on every client, or your chosen LAN IPv4 address must remain stable.

            Monitoring, when enabled: {config.PublicEndpoint.Origin}/monitoring/
            Grafana has its own sign-in. No separate monitoring host port is published.

            If local services are healthy but another device cannot connect, verify both devices
            share a reachable LAN, the chosen host address is current, and the host firewall allows
            TCP {config.PublicEndpoint.HttpsPort}. Check Wi-Fi client isolation and DNS resolution.
            Builder does not change firewall rules, router settings, or client trust stores.

            {certificate.RenewalInstructions}
            """ + "\n";
    }
}
