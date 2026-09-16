using System.Globalization;

namespace VoW.Api.Domain.Mod;

/// <summary>
/// Dotted numeric mod versions ("2.0.3"), compared component by component with missing trailing
/// components treated as zero - the same ordering VersionChecker.parseVersion used client-side.
/// </summary>
public static class ModVersion
{
    public const int MaxLength = 32;

    public static bool TryParse(string? value, out int[] components)
    {
        components = [];
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return false;
        }

        var parts = value.Trim().Split('.');
        var parsed = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var component))
            {
                return false;
            }

            parsed[i] = component;
        }

        components = parsed;
        return true;
    }

    public static bool IsValid(string? value) => TryParse(value, out _);

    public static int Compare(int[] left, int[] right)
    {
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var x = i < left.Length ? left[i] : 0;
            var y = i < right.Length ? right[i] : 0;
            if (x != y)
            {
                return x.CompareTo(y);
            }
        }

        return 0;
    }

    /// <summary>
    /// Mirrors the thresholds the mod applied: disable at or below the kill switch version,
    /// notify at or below the update-notification version.
    /// </summary>
    public static ModUpdateAction DecideAction(string clientVersion, ModRelease release)
    {
        if (!TryParse(clientVersion, out var client))
        {
            return ModUpdateAction.None;
        }

        if (TryParse(release.KillSwitchVersion, out var killSwitch) && Compare(client, killSwitch) <= 0)
        {
            return ModUpdateAction.Disable;
        }

        if (TryParse(release.UpdateNotificationVersion, out var notify) && Compare(client, notify) <= 0)
        {
            return ModUpdateAction.Notify;
        }

        return ModUpdateAction.None;
    }
}
