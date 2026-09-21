namespace RatioForge;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

/// <summary>A data-driven BitTorrent client emulation loaded from clients.json.</summary>
public sealed class ClientProfileDefinition
{
    public required string Name { get; init; }

    public required string UserAgent { get; init; }

    public int DefaultPeerCount { get; init; } = 200;

    public string HttpProtocol { get; init; } = "HTTP/1.1";

    public bool HashUpperCase { get; init; }

    public required ClientRandomValue Key { get; init; }

    public required ClientRandomValue PeerId { get; init; }

    public required string PeerIdPrefix { get; init; }

    public required string Query { get; init; }

    public string[] Headers { get; init; } = [];

    public int KeyRefreshMinutes { get; init; }

    internal TorrentClient CreateClient()
    {
        Validate();
        return new TorrentClient(Name)
        {
            Name = Name,
            HttpProtocol = HttpProtocol,
            HashUpperCase = HashUpperCase,
            Key = Generate(Key),
            PeerID = PeerIdPrefix + Generate(PeerId),
            Query = Query,
            Headers = string.Join("\r\n", Headers) + "\r\n",
            DefNumWant = DefaultPeerCount,
            SearchString = string.Empty,
            ProcessName = string.Empty,
        };
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(UserAgent) ||
            string.IsNullOrWhiteSpace(PeerIdPrefix) || string.IsNullOrWhiteSpace(Query) ||
            Key is null || PeerId is null)
        {
            throw new InvalidDataException("A client profile is missing a required name, user agent, key, peer ID, prefix, or query.");
        }
        if (DefaultPeerCount is < 0 or > 500)
        {
            throw new InvalidDataException($"Client '{Name}' has an invalid default peer count.");
        }

        Key.Validate(Name, "key");
        PeerId.Validate(Name, "peer ID");
    }

    private static string Generate(ClientRandomValue spec)
    {
        const string alphanumeric = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        const string lowerAlphanumeric = "0123456789abcdefghijklmnopqrstuvwxyz";
        const string hexadecimal = "0123456789ABCDEF";
        const string urlSafe = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_.!~*()";
        string value;
        if (spec.Kind == ClientRandomValueKind.TransmissionChecksum)
        {
            var buffer = new char[spec.Length];
            int total = 0;
            for (int index = 0; index < buffer.Length - 1; index++)
            {
                int selected = RandomNumberGenerator.GetInt32(lowerAlphanumeric.Length);
                buffer[index] = lowerAlphanumeric[selected];
                total += selected;
            }

            buffer[^1] = lowerAlphanumeric[(36 - (total % 36)) % 36];
            value = new string(buffer);
        }
        else if (spec.Kind == ClientRandomValueKind.HexRange)
        {
            value = RandomNumberGenerator.GetInt32(int.MaxValue).ToString("x");
        }
        else
        {
            var builder = new StringBuilder(spec.Length);
            string alphabet = spec.Kind switch
            {
                ClientRandomValueKind.Numeric => "0123456789",
                ClientRandomValueKind.Hex => hexadecimal,
                ClientRandomValueKind.LowerAlphanumeric => lowerAlphanumeric,
                ClientRandomValueKind.UrlSafe => urlSafe,
                _ => alphanumeric,
            };
            for (int index = 0; index < spec.Length; index++)
            {
                builder.Append(spec.Kind == ClientRandomValueKind.Random
                    ? (char)RandomNumberGenerator.GetInt32(1, 256)
                    : alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);
            }

            value = builder.ToString();
        }

        if (spec.UrlEncode)
        {
            value = PercentEncode(value, spec.UpperCase);
        }
        else if (spec.UpperCase)
        {
            value = value.ToUpperInvariant();
        }

        return value;
    }

    private static string PercentEncode(string value, bool upperCase)
    {
        var builder = new StringBuilder(value.Length * 3);
        foreach (char character in value)
        {
            if (character < 127 && char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('%').Append(((byte)character).ToString(upperCase ? "X2" : "x2"));
            }
        }

        return builder.ToString();
    }
}

/// <summary>Describes how a client key or peer ID suffix is generated.</summary>
public sealed class ClientRandomValue
{
    [JsonConverter(typeof(JsonStringEnumConverter<ClientRandomValueKind>))]
    public ClientRandomValueKind Kind { get; init; }

    public int Length { get; init; }

    public bool UrlEncode { get; init; }

    public bool UpperCase { get; init; }

    internal void Validate(string clientName, string field)
    {
        if (Kind != ClientRandomValueKind.HexRange && Length is < 1 or > 64)
        {
            throw new InvalidDataException($"Client '{clientName}' has an invalid {field} length.");
        }
    }
}

public enum ClientRandomValueKind
{
    Alphanumeric,
    LowerAlphanumeric,
    Numeric,
    Hex,
    Random,
    UrlSafe,
    TransmissionChecksum,
    HexRange,
}
