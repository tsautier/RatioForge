# Changelog

All notable changes to RatioForge will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-01-18

### Fixed
- **Network**: Fixed HTTPS tracker connections on .NET 8 by integrating `SslStream` support.
- **Upload**: Fixed critical bug where upload speed dropped to 0 if 0 leechers were reported.
- **VersionCheck**: Updated remote version check to use GitHub raw content and improved semantic version parsing.

### Added
- **Emulation**: Added qBittorrent 5.1.2 and uTorrent 3.6.0 emulation profiles.

## [1.0.0] - 2026-01-17

### Major Changes - Project Reborn as RatioForge

This release marks a complete modernization and rebranding of RatioMaster.NET.

#### Added
- **.NET 8 Support**: Complete migration from .NET Framework 4.0 to .NET 8
- **NOTICE.md**: Clear attribution file for original author and derivative work
- **Modern SDK Project Format**: Migrated from legacy .csproj to SDK-style
- **Updated NuGet Packages**:
  - StyleCop.Analyzers 1.0.0 → 1.2.0-beta.556
  - NUnit 3.5.0 → 4.2.2
  - Added Microsoft.NET.Test.Sdk 17.11.1
  - Added NUnit3TestAdapter 4.6.0
- **C# 12 Support**: Leveraging latest language features
- **Nullable Reference Types**: Improved null safety

#### Changed
- **Project Name**: RatioMaster.NET → RatioForge
- **Namespace**: `RatioMaster_source` → `RatioForge`
- **Version**: 0.4.3 → 1.0.0 (fresh start)
- **Assembly Names**: All assemblies renamed to RatioForge
- **Links and URLs**: Updated to point to new repository
- **Copyright**: Added dual copyright (Original: Nikolay Kostov 2006-2016, Fork: tsautier 2026-present)
- **Description**: Updated assembly description to reflect modernization
- **README.md**: Complete rewrite with .NET 8 build instructions
- **LICENSE**: Added tsautier copyright while preserving original

#### Removed
- **packages.config**: Replaced with PackageReference (SDK-style)
- **Old .NET Framework 4.0 dependencies**
- **Obsolete PayPal donation link**
- **Outdated project URLs**

#### Technical Details
- **Target Framework**: .NET Framework 4.0 → .NET 8 (net8.0-windows)
- **Project Format**: Legacy XML → Modern SDK-style
- **Build System**: MSBuild (legacy) → Modern .NET CLI
- **Language Version**: C# 5 → C# 12
- **Platform**: Windows-only (WinForms on .NET 8)

### Migration Notes

This is a **BREAKING CHANGE** release:
- Requires .NET 8 Runtime (not .NET Framework)
- Binary name changed from `RatioMaster.NET.exe` to `RatioForge.exe`
- Configuration files may need migration (different AppData path)
- Some deprecated APIs may behave differently

### Attribution

Based on RatioMaster.NET by Nikolay Kostov (2006-2016)
- Original Repository: https://github.com/NikolayIT/RatioMaster.NET
- Original License: MIT

---

## [0.43] - 2016-01-08 (RatioMaster.NET - Final Release)

*This and earlier versions were published as RatioMaster.NET by Nikolay Kostov*

### Changes
- Made open source on GitHub
- Built for .NET Framework 4.0
- Added new client emulations: uTorrent 3.3.2, 3.3.0, 3.2.0, Transmission 2.82
- Fixed proxies for localhost
- Time displayed in logs can be adjusted to 24h format
- Updated program information and links
- Numerous code refactorings

### Previous Versions (Summary)

- **0.42 (2010-04-19)**: Renamed to RatioMaster.NET, added Vuze support
- **0.41 (2008-08-26)**: Vista memory reader fixes, new client emulations
- **0.40 (2008-01-30)**: Visual Studio 2008 build, Azureus 3.x support
- **0.39 (2007-11-25)**: Fixed numwant/port bugs, new clients
- **0.38 (2007-09-07)**: Performance optimizations, KTorrent support
- **0.37 (2007-07-27)**: Security fixes, ABC/BitComet fixes
- **0.36 (2007-07-15)**: Browser control removed, new settings
- **0.35 (2007-05-10)**: Multiple new client emulations
- **0.34 (2007-02-17)**: Tracker connection fixes
- **0.33 (2007-02-01)**: Finished % bug fixed, BitTyrant support
- **0.32 (2007-01-20)**: Azureus parsing, drag & drop support
- **0.31 (2007-01-05)**: Loading screen added
- **0.30 (2006-12-15)**: Session saving, browser integration
  - *And many more versions dating back to 2006...*

For complete historical changelog, see [HISTORY.TXT](HISTORY.TXT)

---

[1.0.0]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.0
[0.43]: https://github.com/NikolayIT/RatioMaster.NET/releases/tag/v0.43
