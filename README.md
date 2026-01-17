# RatioForge

[![Build status](https://ci.appveyor.com/api/projects/status/16e65svfw87xdolo?svg=true)](https://ci.appveyor.com/project/tsautier/ratioforge)

**RatioForge** is amodern, .NET 8-powered torrent client simulator that allows you to fake upload and download statistics to almost all BitTorrent trackers.

> **Note**: This project is a fork and modernization of [RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) by Nikolay Kostov. See [NOTICE.md](NOTICE.md) for full attribution.

## Features

- **Standalone Application**: Does NOT rely on your BitTorrent client (uTorrent, qBittorrent, etc.)
- **No Real Transfer**: Does NOT download/upload actual files - only simulates stats
- **Wide Client Support**: Hardcoded emulations for popular BitTorrent clients:
  - uTorrent (multiple versions)
  - BitComet, Azureus/Vuze
  - ABC, BitLord, BTuga
  - BitTornado, Burst, BitTyrant, BitSpirit
  - Deluge, Transmission, KTorrent
  - And more!
- **Modernized**: Rebuilt for .NET 8 with improved performance and Windows 11 support

## Requirements

- **Windows 10/11** (64-bit recommended)
- **.NET 8 Runtime** ([Download here](https://dotnet.microsoft.com/download/dotnet/8.0))

## Installation

1. Download the latest release from the [Releases](https://github.com/tsautier/RatioForge/releases) page
2. Extract the archive
3. Run `RatioForge.exe`

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (or later) or Rider

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
dotnet run --project Source/RatioForge/RatioForge.csproj
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

For detailed usage instructions, see the built-in help menu.

## What's New in 1.0.0 (RatioForge)

- **Complete Rename**: RatioMaster.NET → RatioForge
- **.NET 8 Migration**: Modernized from .NET Framework 4.0
- **Improved Performance**: Leveraging latest .NET runtime optimizations
- **Updated Dependencies**: All packages upgraded to latest versions
- **Modern Codebase**: C# 12 features and nullable reference types
- **Better Maintainability**: SDK-style project format

See [CHANGELOG.md](CHANGELOG.md) for full version history.

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
- **Derivative Work** (2026-present): Copyright © tsautier

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

**Made with ❤️ using .NET 8**  
**Based on RatioMaster.NET by Nikolay Kostov**