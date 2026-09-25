using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VoW.Api.Services.Casting;

public sealed record CccSubmission(string RoleName, string Username, string AudioUrl);

public class CccException(string message) : Exception(message);

public sealed class CccAuthException()
    : CccException("Casting Call Club rejected the token. Update CCC_TOKEN on the API and try again.");

public interface ICccClient
{
    Task<IReadOnlyList<CccSubmission>> GetUnsortedSubmissionsAsync(string projectUrl, CancellationToken cancellationToken);

    Task<Stream> DownloadAudioAsync(string audioUrl, CancellationToken cancellationToken);
}

/// <summary>
/// Reads a Casting Call Club project's unsorted submissions with the project owner's token. Ported from
/// VowBot's /setuppoll: find the project id on the public casting page, trade the persistent _ccc_token
/// for a _ccc_session, then page through the manage API until a page is empty or repeats itself.
/// </summary>
public sealed partial class CccClient(HttpClient httpClient, IConfiguration configuration) : ICccClient
{
    private const string CccHost = "www.castingcall.club";
    private const int MaxPages = 200;
    private const string UserAgent = "Mozilla/5.0 (compatible; VoicesOfWynn)";

    public static bool IsCccUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host == CccHost || uri.Host == "castingcall.club");

    public async Task<IReadOnlyList<CccSubmission>> GetUnsortedSubmissionsAsync(
        string projectUrl,
        CancellationToken cancellationToken)
    {
        if (!IsCccUrl(projectUrl))
        {
            throw new CccException("Only https://www.castingcall.club links can be imported.");
        }

        var token = configuration["CCC_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CccException("CCC_TOKEN is not configured on the API.");
        }

        var projectId = await FindProjectIdAsync(projectUrl, cancellationToken);
        var session = await FetchSessionCookieAsync(token, cancellationToken);
        var cookie = $"_ccc_token={token}; _ccc_session={session}";

        var submissions = new List<CccSubmission>();
        string? previousPage = null;
        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"https://{CccHost}/api/v3/manage/projects/{projectId}/submissions?order_by=updated_at&review_status=unsorted&page={page}";
            var json = await GetJsonAsync(url, cookie, cancellationToken);
            var (pageSubmissions, rawArray) = ParseSubmissionsPage(json);
            if (pageSubmissions.Count == 0 || rawArray == previousPage)
            {
                break;
            }

            previousPage = rawArray;
            submissions.AddRange(pageSubmissions);
        }

        return submissions;
    }

    public async Task<Stream> DownloadAudioAsync(string audioUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new CccException($"Refusing to download a non-https audio url: {audioUrl}");
        }

        var response = await SendFollowingRedirectsAsync(uri, allowAnyHost: true, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new CccException($"Downloading audio failed with status {(int)response.StatusCode}.");
        }

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <summary>
    /// Parses one page of the manage API. Returns the submissions plus the raw array text, which the
    /// caller compares against the previous page because CCC repeats the last page instead of ending.
    /// </summary>
    public static (IReadOnlyList<CccSubmission> Submissions, string RawArray) ParseSubmissionsPage(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("submissions", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            throw new CccException("Casting Call Club returned an unexpected response (no submissions list).");
        }

        var submissions = new List<CccSubmission>();
        foreach (var item in array.EnumerateArray())
        {
            var role = GetString(item, "roleName")?.Trim();
            var audio = GetString(item, "audioUrl");
            if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(audio))
            {
                continue;
            }

            var username = GetString(item, "username")?.Trim();
            submissions.Add(new CccSubmission(role, string.IsNullOrEmpty(username) ? "unknown" : username, audio));
        }

        return (submissions, array.GetRawText());
    }

    public static string? ExtractProjectId(string html)
    {
        var match = ProjectIdRegex().Match(html);
        return match.Success ? match.Groups["id"].Value : null;
    }

    private async Task<string> FindProjectIdAsync(string projectUrl, CancellationToken cancellationToken)
    {
        using var response = await SendFollowingRedirectsAsync(
            new Uri(projectUrl), allowAnyHost: false, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new CccException($"Opening the casting page failed with status {(int)response.StatusCode}.");
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractProjectId(html)
               ?? throw new CccException("Could not find the project id on that page. Is it a casting call you own?");
    }

    private async Task<string> FetchSessionCookieAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{CccHost}/");
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Add("Cookie", $"_ccc_token={token}");
        using var response = await httpClient.SendAsync(request, cancellationToken);

        var status = (int)response.StatusCode;
        if (response.StatusCode != HttpStatusCode.OK && status is < 300 or >= 400)
        {
            throw new CccException($"Casting Call Club sign-in failed with status {status}.");
        }

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith("_ccc_session=", StringComparison.Ordinal))
                {
                    return cookie.Split(';')[0]["_ccc_session=".Length..];
                }
            }
        }

        throw new CccAuthException();
    }

    private async Task<string> GetJsonAsync(string url, string cookie, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new CccAuthException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new CccException($"Casting Call Club returned status {(int)response.StatusCode} while listing submissions.");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// The handler does not follow redirects (the session cookie arrives on a redirect response), so the
    /// page and audio downloads follow them here, https only and, for pages, only within CCC.
    /// </summary>
    private async Task<HttpResponseMessage> SendFollowingRedirectsAsync(
        Uri uri,
        bool allowAnyHost,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        for (var hop = 0; ; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            var response = await httpClient.SendAsync(request, completionOption, cancellationToken);
            var status = (int)response.StatusCode;
            if (status is < 300 or >= 400 || response.Headers.Location is null)
            {
                return response;
            }

            response.Dispose();
            var next = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(uri, response.Headers.Location);
            if (hop >= 5 || next.Scheme != Uri.UriSchemeHttps || (!allowAnyHost && !IsCccUrl(next.ToString())))
            {
                throw new CccException($"Refusing to follow a redirect to {next}.");
            }

            uri = next;
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [GeneratedRegex(@"[?&]project_id=(?<id>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectIdRegex();
}
