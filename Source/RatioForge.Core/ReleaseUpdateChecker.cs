namespace RatioForge;

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed record ReleaseAsset(string Name, string DownloadUrl, long Size);

/// <summary>Describes the latest published GitHub release relative to the running application.</summary>
public sealed record ReleaseUpdateResult(
    Version CurrentVersion,
    Version LatestVersion,
    bool IsUpdateAvailable,
    string ReleaseUrl,
    string ReleaseNotes = "",
    ReleaseAsset? Package = null,
    ReleaseAsset? Checksum = null);

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

        string releaseNotes = document.RootElement.TryGetProperty("body", out JsonElement bodyElement)
            ? bodyElement.GetString() ?? string.Empty
            : string.Empty;
        IReadOnlyList<ReleaseAsset> assets = ReadAssets(document.RootElement);
        string runtime = CurrentRuntimeIdentifier();
        string archiveSuffix = OperatingSystem.IsWindows() ? ".zip" : ".tar.gz";
        ReleaseAsset? package = assets.FirstOrDefault(asset =>
            asset.Name.Contains($"-{runtime}", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith(archiveSuffix, StringComparison.OrdinalIgnoreCase));
        ReleaseAsset? checksum = assets.FirstOrDefault(asset =>
            asset.Name.EndsWith($"-{runtime}.sha256", StringComparison.OrdinalIgnoreCase));

        return new ReleaseUpdateResult(
            parsedCurrentVersion,
            latestVersion,
            latestVersion > parsedCurrentVersion,
            releaseUrl,
            releaseNotes,
            package,
            checksum);
    }

    private static IReadOnlyList<ReleaseAsset> ReadAssets(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out JsonElement assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return assetsElement.EnumerateArray()
            .Select(asset => new ReleaseAsset(
                asset.GetProperty("name").GetString() ?? string.Empty,
                asset.GetProperty("browser_download_url").GetString() ?? string.Empty,
                asset.TryGetProperty("size", out JsonElement size) ? size.GetInt64() : 0))
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name) &&
                Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out _))
            .ToArray();
    }

    internal static string CurrentRuntimeIdentifier()
    {
        string architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => throw new PlatformNotSupportedException("Only x64 and arm64 updates are published."),
        };
        string platform = OperatingSystem.IsWindows() ? "win" :
            OperatingSystem.IsMacOS() ? "osx" :
            OperatingSystem.IsLinux() ? "linux" :
            throw new PlatformNotSupportedException("Automatic downloads are not available for this platform.");
        return $"{platform}-{architecture}";
    }

    private static HttpClient CreateClient() => new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };
}

/// <summary>Downloads the selected release package and verifies it against the published SHA256 manifest.</summary>
public sealed class ReleasePackageDownloader(HttpClient? client = null)
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly HttpClient client = client ?? SharedClient;

    public async Task<string> DownloadAndVerifyAsync(
        ReleaseUpdateResult release,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (release.Package is null || release.Checksum is null)
        {
            throw new InvalidOperationException("This release has no compatible package or checksum manifest.");
        }

        Directory.CreateDirectory(destinationDirectory);
        string safeFileName = Path.GetFileName(release.Package.Name);
        if (!safeFileName.Equals(release.Package.Name, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The release package name is unsafe.");
        }

        string targetPath = Path.Combine(destinationDirectory, safeFileName);
        string partialPath = targetPath + ".download";
        try
        {
            byte[] manifest = await client.GetByteArrayAsync(release.Checksum.DownloadUrl, cancellationToken)
                .ConfigureAwait(false);
            string expectedHash = ParseExpectedHash(System.Text.Encoding.UTF8.GetString(manifest), release.Package.Name);
            await using (Stream source = await client.GetStreamAsync(release.Package.DownloadUrl, cancellationToken).ConfigureAwait(false))
            await using (var destination = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            string actualHash;
            await using (FileStream package = File.OpenRead(partialPath))
            {
                if (release.Package.Size > 0 && package.Length != release.Package.Size)
                {
                    throw new InvalidDataException("The downloaded update size does not match the GitHub release metadata.");
                }

                actualHash = Convert.ToHexString(await SHA256.HashDataAsync(package, cancellationToken)).ToLowerInvariant();
            }

            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The downloaded update failed SHA256 verification.");
            }

            File.Move(partialPath, targetPath, overwrite: true);
            return targetPath;
        }
        finally
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
        }
    }

    internal static string ParseExpectedHash(string manifest, string fileName)
    {
        foreach (string line in manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[^1].TrimStart('*').Equals(fileName, StringComparison.Ordinal))
            {
                return parts[0];
            }
        }

        throw new InvalidDataException($"The checksum manifest has no entry for '{fileName}'.");
    }
}
