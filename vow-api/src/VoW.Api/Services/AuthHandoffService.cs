using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using VoW.Api.Contracts.Auth;

namespace VoW.Api.Services;

public sealed class AuthHandoffService : IAuthHandoffService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, HandoffEntry> entries = new();

    public string Create(AuthTokenResponse tokens)
    {
        PruneExpired();

        var bytes = RandomNumberGenerator.GetBytes(32);
        var code = WebEncoders.Base64UrlEncode(bytes);
        entries[code] = new HandoffEntry(tokens, DateTimeOffset.UtcNow.Add(Lifetime));
        return code;
    }

    public AuthTokenResponse? Consume(string code)
    {
        if (!entries.TryRemove(code, out var entry))
        {
            return null;
        }

        return entry.ExpiresAt < DateTimeOffset.UtcNow ? null : entry.Tokens;
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (code, entry) in entries)
        {
            if (entry.ExpiresAt < now)
            {
                entries.TryRemove(code, out _);
            }
        }
    }

    private sealed record HandoffEntry(AuthTokenResponse Tokens, DateTimeOffset ExpiresAt);
}
