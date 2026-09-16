using System.Net.Http.Json;
using System.Text;

namespace VoW.Api.Services.Npcs;

public sealed class DiscordCommentNotifier(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordCommentNotifier> logger) : ICommentNotifier
{
    // Discord truncates webhook usernames past 80 characters and rejects some values outright.
    private const int UsernameMaxLength = 64;

    public async Task NotifyAsync(CommentNotification notification, CancellationToken cancellationToken)
    {
        var webhookUrl = configuration["DISCORD_COMMENTS_WEBHOOK_URL"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        var payload = new
        {
            content = Body(notification),
            username = Username(notification.AuthorName),
            avatar_url = notification.AvatarUrl,
            // Without this, writing "@everyone" in a comment pings the whole Discord server.
            // Comment text is anonymous user input, so mentions are never honoured.
            allowed_mentions = new { parse = Array.Empty<string>() },
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Discord rejected the notification for comment {CommentId} with status {Status}.",
                    notification.CommentId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // The comment is already committed; Discord is only a notification channel.
            logger.LogWarning(ex, "Could not announce comment {CommentId} on Discord.", notification.CommentId);
        }
    }

    private static string Body(CommentNotification notification)
    {
        var builder = new StringBuilder()
            .Append("New comment has been posted on the following NPC: `")
            .Append(Inline(notification.NpcName))
            .Append(" (ID #")
            .Append(notification.NpcId)
            .Append(")`");

        // Quoting each line keeps the comment visually separate and stops a leading "#" or "-"
        // from being read as Discord markdown.
        foreach (var line in notification.Content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            builder.Append("\n> ").Append(Inline(line.Trim()));
        }

        return builder.ToString();
    }

    /// <summary>Neutralizes the backticks that would otherwise break out of inline code spans.</summary>
    private static string Inline(string value) => value.Replace("`", "'", StringComparison.Ordinal);

    private static string Username(string authorName)
    {
        var name = Inline(authorName).Replace("\n", " ", StringComparison.Ordinal);
        var username = $"{name} via voicesofwynn.com";
        return username.Length <= UsernameMaxLength ? username : username[..UsernameMaxLength];
    }
}
