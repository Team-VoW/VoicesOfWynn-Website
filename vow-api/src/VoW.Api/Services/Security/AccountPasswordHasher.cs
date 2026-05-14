namespace VoW.Api.Services.Security;

public static class AccountPasswordHasher
{
    public static string Hash(string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        return hash.StartsWith("$2a$", StringComparison.Ordinal) || hash.StartsWith("$2b$", StringComparison.Ordinal)
            ? $"$2y${hash[4..]}"
            : hash;
    }

    public static bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, Normalize(hash));
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    private static string Normalize(string hash) =>
        hash.StartsWith("$2y$", StringComparison.Ordinal)
            ? $"$2a${hash[4..]}"
            : hash;
}
