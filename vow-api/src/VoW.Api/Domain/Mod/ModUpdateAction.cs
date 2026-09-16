using System.Text.Json.Serialization;

namespace VoW.Api.Domain.Mod;

/// <summary>
/// What the client should do about its version. The comparison used to live in the mod
/// (VersionChecker.compareVersions), which meant a kill switch could only ever be as reliable as
/// the already-shipped client's own parsing. The server decides now.
/// </summary>
/// <remarks>
/// The wire values are pinned lowercase rather than left to the globally registered
/// <c>JsonStringEnumConverter</c>: mod clients match on this string, and a shipped jar cannot be
/// corrected if the casing ever shifts.
/// </remarks>
public enum ModUpdateAction
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("notify")]
    Notify,

    [JsonStringEnumMemberName("disable")]
    Disable
}
