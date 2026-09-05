using System.Text.RegularExpressions;

namespace VoW.Api.Services.Feedback;

public static partial class QuestNameNormalizer
{
    public static string Normalize(string name) => Whitespace().Replace(Formatting().Replace(name, ""), " ").Trim().ToUpperInvariant();
    [GeneratedRegex("§[0-9a-fk-orx]", RegexOptions.IgnoreCase)] private static partial Regex Formatting();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
}
