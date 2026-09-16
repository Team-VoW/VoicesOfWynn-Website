using System.Security.Cryptography;
using System.Text;

namespace VoW.Api.Services.Npcs;

/// <summary>
/// Derives the opaque voter id stored in <c>vote.voter</c>.
/// </summary>
/// <remarks>
/// The column is <c>varchar(64) ascii</c> holding 64 hex characters of SHA-256, and the legacy site
/// fills it with <c>sha256(minecraft_uuid ?? REMOTE_ADDR)</c>. Anonymous visitors keep that exact
/// shape so their existing votes still count. Signed-in contributors are keyed on their account
/// instead, which is stable across networks and is the only identity here that a shared address
/// cannot collapse - anonymous visitors behind one NAT still share a vote.
/// </remarks>
public static class VoterIdentity
{
    public static string ForUser(int userId) => Hash($"u:{userId}");

    public static string ForAddress(string ipAddress) => Hash(ipAddress);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
