using Ganss.Xss;

namespace VoW.Api.Services.Accounts;

internal static class AccountBioSanitizer
{
    public static string? Sanitize(string? bio)
    {
        if (bio is null)
        {
            return null;
        }

        var normalized = bio.Replace("<br>", "<br />", StringComparison.Ordinal);
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(
            [
                "p",
                "div",
                "span",
                "strong",
                "em",
                "ul",
                "ol",
                "li",
                "h1",
                "h2",
                "h3",
                "a",
                "img",
                "br",
            ]);

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(
            [
                "style",
                "title",
                "href",
                "target",
                "rel",
                "src",
                "alt",
                "width",
                "height",
                "data-mce-style",
                "data-mce-href",
                "data-mce-selected",
                "data-mce-src",
            ]);

        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.UnionWith(["text-align", "text-decoration"]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);

        var sanitized = sanitizer.Sanitize(normalized).Trim();
        return sanitized.Length == 0 ? null : sanitized;
    }
}
