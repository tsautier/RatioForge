namespace RatioForge;

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Describes the latest published GitHub release relative to the running application.</summary>
public sealed record ReleaseUpdateResult(
    Version CurrentVersion,
    Version LatestVersion,
    bool IsUpdateAvailable,
    string ReleaseUrl);

/// <summary>Checks the latest published, non-prerelease RatioForge release on GitHub.</summary>
public sealed class ReleaseUpdateChecker
{
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/tsautier/RatioForge/releases/latest";
    public const string LatestReleasePageUrl = "https://github.com/tsautier/RatioForge/releases/latest";

    private static readonly HttpClient SharedClient = CreateClient();
    private readonly HttpClient client;

    public ReleaseUpdateChecker(HttpClient? client = null)
    {
        this.client = client ?? SharedClient;
    }

    public async Task<ReleaseUpdateResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (!Version.TryParse(currentVersion, out Version? parsedCurrentVersion))
        {
            throw new ArgumentException("The current application version is invalid.", nameof(currentVersion));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd($"RatioForge/{parsedCurrentVersion}");

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);

        string tag = document.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
        string releaseUrl = document.RootElement.TryGetProperty("html_url", out JsonElement urlElement)
            ? urlElement.GetString() ?? LatestReleasePageUrl
            : LatestReleasePageUrl;
        string normalizedTag = tag.StartsWith('v') ? tag[1..] : tag;
        if (!Version.TryParse(normalizedTag, out Version? latestVersion))
        {
            throw new InvalidDataException($"GitHub returned an invalid release tag: '{tag}'.");
        }

        return new ReleaseUpdateResult(
            parsedCurrentVersion,
            latestVersion,
            latestVersion > parsedCurrentVersion,
            releaseUrl);
    }

    private static HttpClient CreateClient() => new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };
}
