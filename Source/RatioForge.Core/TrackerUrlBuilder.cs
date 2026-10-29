namespace RatioForge
{
    using System;
    using System.Net;
    using System.Text;

    /// <summary>
    /// Builds tracker announce and scrape URLs independently from the Windows Forms UI.
    /// </summary>
    internal static class TrackerUrlBuilder
    {
        internal static string BuildAnnounce(TorrentInfo torrentInfo, TorrentClient client, string eventType, string localIp)
        {
            ArgumentNullException.ThrowIfNull(client);

            string uploaded = torrentInfo.uploaded > 0
                ? RoundByDenominator(torrentInfo.uploaded, 0x4000).ToString()
                : "0";
            string downloaded = torrentInfo.downloaded > 0
                ? RoundByDenominator(torrentInfo.downloaded, 0x10).ToString()
                : "0";
            long left = Math.Max(0, torrentInfo.left);
            string numberOfPeers = eventType.Contains("stopped", StringComparison.OrdinalIgnoreCase)
                ? "0"
                : torrentInfo.numberOfPeers == "0" ? "200" : torrentInfo.numberOfPeers;

            string url = AppendQuerySeparator(torrentInfo.tracker);
            if (eventType.Contains("started", StringComparison.Ordinal))
            {
                url = url.Replace("&natmapped=1&localip={localip}", string.Empty, StringComparison.Ordinal);
            }

            if (!eventType.Contains("stopped", StringComparison.Ordinal))
            {
                url = url.Replace("&trackerid=48", string.Empty, StringComparison.Ordinal);
            }

            return (url + client.Query)
                .Replace("{infohash}", EncodeHash(torrentInfo.hash, client.HashUpperCase), StringComparison.Ordinal)
                .Replace("{peerid}", torrentInfo.peerID, StringComparison.Ordinal)
                .Replace("{port}", torrentInfo.port, StringComparison.Ordinal)
                .Replace("{uploaded}", uploaded, StringComparison.Ordinal)
                .Replace("{downloaded}", downloaded, StringComparison.Ordinal)
                .Replace("{left}", left.ToString(), StringComparison.Ordinal)
                .Replace("{event}", eventType, StringComparison.Ordinal)
                .Replace("{numwant}", numberOfPeers, StringComparison.Ordinal)
                .Replace("{key}", torrentInfo.key, StringComparison.Ordinal)
                .Replace("{localip}", EncodeIpAddress(localIp), StringComparison.Ordinal);
        }

        internal static string BuildScrape(TorrentInfo torrentInfo, TorrentClient client)
        {
            ArgumentNullException.ThrowIfNull(client);

            int announceIndex = torrentInfo.tracker.LastIndexOf("announce", StringComparison.OrdinalIgnoreCase);
            if (announceIndex == -1)
            {
                return string.Empty;
            }

            string scrapeUrl = torrentInfo.tracker.Substring(0, announceIndex)
                + "scrape"
                + torrentInfo.tracker.Substring(announceIndex + "announce".Length);
            return AppendQuerySeparator(scrapeUrl) + "info_hash=" + EncodeHash(torrentInfo.hash, client.HashUpperCase);
        }

        internal static string EncodeHash(string hexadecimalHash, bool upperCase)
        {
            ArgumentException.ThrowIfNullOrEmpty(hexadecimalHash);
            if (hexadecimalHash.Length % 2 != 0)
            {
                throw new FormatException("A hexadecimal hash must contain an even number of characters.");
            }

            var encoded = new StringBuilder(hexadecimalHash.Length * 3 / 2);
            for (int index = 0; index < hexadecimalHash.Length; index += 2)
            {
                byte value = Convert.ToByte(hexadecimalHash.Substring(index, 2), 16);
                char character = (char)value;
                if (value < 127 && char.IsLetterOrDigit(character))
                {
                    encoded.Append(character);
                }
                else
                {
                    encoded.Append('%');
                    encoded.Append(value.ToString(upperCase ? "X2" : "x2"));
                }
            }

            return encoded.ToString();
        }

        internal static string EncodeIpAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return string.Empty;
            }

            if (!IPAddress.TryParse(address, out IPAddress parsed))
            {
                throw new FormatException("The local address must be a valid IPv4 or IPv6 address.");
            }

            return Uri.EscapeDataString(parsed.ToString());
        }

        private static string AppendQuerySeparator(string url)
        {
            return url + (url.Contains("?", StringComparison.Ordinal) ? "&" : "?");
        }

        private static long RoundByDenominator(long value, long denominator)
        {
            return denominator * (value / denominator);
        }
    }
}
