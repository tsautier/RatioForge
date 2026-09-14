namespace RatioForge;

using System.Text.RegularExpressions;

/// <summary>Removes credentials and tracker passkeys from diagnostic text.</summary>
public static class SensitiveDataRedactor
{
    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "apikey",
        "api_key",
        "auth",
        "authkey",
        "credential",
        "keypass",
        "password",
        "passkey",
        "secret",
        "token",
    };

    private static readonly Regex UrlPattern = new(
        @"(?:https?|socks5)://[^\s\""']+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex AssignmentPattern = new(
        @"(?<![a-z0-9_])(?<key>password|passkey|token|api_?key|authkey|secret|key|peer_?id|info_?hash)\s*(?<separator>[=:])\s*[^\s;&]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex HexTokenPattern = new(
        @"^[a-f0-9]{20,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex LongTokenPattern = new(
        @"^[a-z0-9_-]{32,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return RedactAssignments(UrlPattern.Replace(text, match => RedactMatchedUrl(match.Value)));
    }

    public static string RedactUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return RedactAssignments(value ?? string.Empty);
        }

        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            builder.UserName = "REDACTED";
            builder.Password = string.Empty;
        }

        string[] segments = uri.AbsolutePath.Split('/');
        for (int index = 0; index < segments.Length; index++)
        {
            string decoded = Uri.UnescapeDataString(segments[index]);
            if (HexTokenPattern.IsMatch(decoded) || LongTokenPattern.IsMatch(decoded))
            {
                segments[index] = "REDACTED";
            }
        }

        builder.Path = string.Join('/', segments);
        if (!string.IsNullOrEmpty(uri.Query))
        {
            builder.Query = string.Join('&', uri.Query[1..].Split('&').Select(RedactQueryPart));
        }

        return builder.Uri.AbsoluteUri;
    }

    private static string RedactMatchedUrl(string matchedUrl)
    {
        string suffix = string.Empty;
        while (matchedUrl.Length > 0 && matchedUrl[^1] is '.' or ',' or ')' or ']' or ';')
        {
            suffix = matchedUrl[^1] + suffix;
            matchedUrl = matchedUrl[..^1];
        }

        return RedactUrl(matchedUrl) + suffix;
    }

    private static string RedactQueryPart(string part)
    {
        int separator = part.IndexOf('=');
        string encodedKey = separator >= 0 ? part[..separator] : part;
        string key = Uri.UnescapeDataString(encodedKey.Replace('+', ' '));
        return SensitiveQueryKeys.Contains(key) ? encodedKey + "=REDACTED" : part;
    }

    private static string RedactAssignments(string value) => AssignmentPattern.Replace(
        value,
        match => $"{match.Groups["key"].Value}{match.Groups["separator"].Value}REDACTED");
}
