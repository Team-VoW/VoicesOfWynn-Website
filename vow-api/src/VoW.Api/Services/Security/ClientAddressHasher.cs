using System.Security.Cryptography;
using System.Text;

namespace VoW.Api.Services.Security;

/// <summary>
/// Hashes a caller's IP address before it is stored against a bootup ping. The PHP app stored an
/// unsalted SHA-256, which for IPv4 is a 4-billion-entry rainbow table away from being plaintext.
/// A salt configured as ANALYTICS_IP_SALT fixes that; without one the behaviour stays unsalted so
/// that the ping throttle keeps matching rows written before the migration.
/// </summary>
public sealed class ClientAddressHasher(IConfiguration configuration)
{
    private readonly string _salt = configuration["ANALYTICS_IP_SALT"] ?? string.Empty;

    public string Hash(string address) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(_salt + address)));
}
