using System.Net.Mail;
using VoW.Api.Contracts.Npcs;

namespace VoW.Api.Services.Npcs;

/// <summary>A comment that has passed validation, with its fields already normalized.</summary>
public sealed record ValidatedComment(string? Name, string? Email, string Content);

public static class NpcCommentValidator
{
    // user.name is varchar(31) and user.email varchar(255). Content is capped far below the
    // column's 64 KB: a comment that long is an abuse vector rather than a feature, and the same
    // 2000 characters are what quest feedback already allows.
    private const int NameMaxLength = 31;
    private const int EmailMaxLength = 255;
    public const int ContentMaxLength = 2000;

    public static bool TryValidate(
        PostNpcCommentRequest request,
        bool authenticated,
        out ValidatedComment comment,
        out IReadOnlyDictionary<string, string> errors)
    {
        var failures = new Dictionary<string, string>();
        comment = new ValidatedComment(null, null, string.Empty);

        var content = request.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
        {
            failures[nameof(request.Content)] = "A comment cannot be empty.";
        }
        else if (content.Length > ContentMaxLength)
        {
            failures[nameof(request.Content)] = $"A comment must not exceed {ContentMaxLength} characters.";
        }

        // A signed-in contributor's name and avatar come from their account, so anything the
        // client sends alongside is ignored rather than trusted.
        string? name = null;
        string? email = null;
        if (!authenticated)
        {
            name = Normalize(request.Name);
            if (name is not null && name.Length > NameMaxLength)
            {
                failures[nameof(request.Name)] = $"A name must not exceed {NameMaxLength} characters.";
            }
            else if (name is not null && name.Any(char.IsControl))
            {
                failures[nameof(request.Name)] = "A name must not contain control characters.";
            }

            email = Normalize(request.Email);
            if (email is not null && email.Length > EmailMaxLength)
            {
                failures[nameof(request.Email)] = $"An e-mail address must not exceed {EmailMaxLength} characters.";
            }
            else if (email is not null && !MailAddress.TryCreate(email, out _))
            {
                failures[nameof(request.Email)] = "That does not look like an e-mail address.";
            }
        }

        errors = failures;
        if (failures.Count > 0)
        {
            return false;
        }

        comment = new ValidatedComment(name, email, content);
        return true;
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
