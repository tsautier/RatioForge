# RatioForge

[![Build RatioForge](https://github.com/tsautier/RatioForge/actions/workflows/build.yml/badge.svg)](https://github.com/tsautier/RatioForge/actions/workflows/build.yml)
[![Release RatioForge](https://github.com/tsautier/RatioForge/actions/workflows/release.yml/badge.svg)](https://github.com/tsautier/RatioForge/actions/workflows/release.yml)

**RatioForge** is a modern, .NET 10-powered torrent client simulator that allows you to simulate upload and download statistics with BitTorrent trackers.

> **Note**: This project is a fork and modernization of [RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) by Nikolay Kostov. See [NOTICE.md](NOTICE.md) for full attribution.

## Features

- **Standalone Application**: Does NOT rely on your BitTorrent client (uTorrent, qBittorrent, etc.)
- **No Real Transfer**: Does NOT download/upload actual files - only simulates stats
- **Wide Client Support**: Data-driven emulations for popular BitTorrent clients:
  - uTorrent (multiple versions)
  - BitComet, Azureus/Vuze, BiglyBT
  - ABC, BitLord, BTuga
  - BitTornado, Burst, BitTyrant, BitSpirit
  - Deluge, Transmission, KTorrent
  - And more!
- **Current default identity**: qBittorrent 5.2.3, the latest stable release verified by the project
- **Persistent settings**: Cross-platform session defaults and named profiles with JSON import/export, automatic/dark/light appearance, IPv4/IPv6 source selection, HTTP/SOCKS5 proxy, speed randomization, activity logging, and opt-in debug logs
- **Release updates**: Automatic and manual checks with release notes, platform-aware downloads, and SHA256 verification
- **Session controls**: Immediate manual announces, clean reset, lifecycle events, live completion/ratio/time, and configurable automatic stopping
- **Diagnostics**: Privacy-filtered activity/debug logs, filterable CSV/JSON announce history with per-attempt request details, and pre-session DNS/IP/TLS/proxy checks
- **Cross-platform**: Native desktop builds for Windows, Linux, and macOS
- **Tracker networking**: HTTP, HTTPS, and UDP trackers over IPv4 or IPv6, `announce-list` tiers, failover, automatic routing, and explicit local-address binding
- **Modernized**: Rebuilt for .NET 10 and Avalonia UI

## Requirements

- **Windows 10/11 x64**, **Linux x64**, or **macOS 15+** on Intel or Apple Silicon
- Self-contained downloads do not require a separate .NET installation.
- Much smaller Lite downloads require the [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) for the matching platform and architecture.

## Installation

1. Download the latest release from the [Releases](https://github.com/tsautier/RatioForge/releases) page.
2. Choose the asset for `win-x64`, `linux-x64`, `osx-x64`, or `osx-arm64`.
3. Choose the `-lite` executable for the smallest download when .NET 10 is already installed.
4. Otherwise choose the self-contained executable or archive. Linux uses `tar.gz`, macOS archives contain `RatioForge.app`, and Windows uses `zip`.
5. On Linux or macOS, make the raw executable runnable after download with `chmod +x RatioForge-*`.

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026 version 18.0 or later, or Rider with .NET 10 support

### Build Steps

```bash
# Clone the repository
git clone https://github.com/tsautier/RatioForge.git
cd RatioForge

# Restore dependencies
dotnet restore Source/RatioForge.sln

# Build
dotnet build Source/RatioForge.sln --configuration Release

# Run
dotnet run --project Source/RatioForge.Desktop/RatioForge.Desktop.csproj
```

### Running Tests

```bash
dotnet test Source/RatioForge.sln
```

## Usage

1. Load a `.torrent` file
2. Select the BitTorrent client to emulate
3. Configure upload/download speeds and ratio
4. Click "Start" to begin sending fake stats to the tracker

For support, use the built-in Help menu to open the GitHub repository or create an issue.

### Custom client profiles

RatioForge loads its built-in emulations from `clients.json`. Settings can export the effective catalog and import a versioned replacement without rebuilding. The imported file is installed at the configuration path shown in Settings; existing names are replaced and new names are appended immediately.

```json
{
  "clients": [
    {
      "name": "ExampleClient 1.0",
      "userAgent": "ExampleClient/1.0",
      "defaultPeerCount": 200,
      "key": { "kind": "Hex", "length": 8, "upperCase": true },
      "peerIdPrefix": "-EX1000-",
      "peerId": { "kind": "UrlSafe", "length": 12 },
      "headers": ["Host: {host}", "User-Agent: ExampleClient/1.0", "Connection: close"],
      "query": "info_hash={infohash}&peer_id={peerid}&port={port}&uploaded={uploaded}&downloaded={downloaded}&left={left}&key={key}{event}&numwant={numwant}&compact=1"
    }
  ]
}
```

Supported random kinds are `Alphanumeric`, `LowerAlphanumeric`, `Numeric`, `Hex`, `Random`, `UrlSafe`, `TransmissionChecksum`, and `HexRange`. A malformed custom file is ignored, reported in Activity, and never replaces the built-in catalog.

## What's New in 1.2.2 (RatioForge)

- **Compatibility guardrails**: Differential tests compare refactored announces with historical parameter output
- **Portable profiles**: Import and export session and client profiles from Settings without exposing proxy passwords
- **Attempt diagnostics**: Inspect and copy a privacy-filtered request and protocol diagnostic for every announce attempt
- **Broader fixtures**: Cover CDN-style passkeys, IPv6 UDP, complex tracker tiers, nested paths, and large torrents
- **Measured releases**: Strict Lite size budgets and per-runtime startup, memory, and size metrics

See [CHANGELOG.md](CHANGELOG.md) for full version history.
The current compatibility review is recorded in the [September 2026 client profile audit](docs/client-profile-audit-2026-09-21.md).

## History

**RatioForge** is based on **RatioMaster.NET** by Nikolay Kostov:
- Original Development: 2006-2016
- Original Author: [Nikolay Kostov (NikolayIT)](http://nikolay.it)
- Original Repository: https://github.com/NikolayIT/RatioMaster.NET

This fork was created in 2026 to modernize the project for current .NET standards while preserving all original functionality.

## License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

### Attribution

- **Original Work** (2006-2016): Copyright © Nikolay Kostov
- **Derivative Work** (2026-present): Copyright © Thomas SAUTIER

See [NOTICE.md](NOTICE.md) for full attribution details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Disclaimer

This software is for educational purposes only. Use at your own risk. The developers are not responsible for any misuse or damage caused by this program.

## Support

- **Issues**: [GitHub Issues](https://github.com/tsautier/RatioForge/issues)
- **Discussions**: [GitHub Discussions](https://github.com/tsautier/RatioForge/discussions)

---

**Made with ❤️ using .NET 10**

**Based on RatioMaster.NET by Nikolay Kostov**
