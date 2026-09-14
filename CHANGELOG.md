# Changelog

All notable changes to RatioForge will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.1] - 2026-09-14

### Added
- **Appearance:** Added automatic system theme detection plus persistent manual Dark and Light modes.
- **Updates:** Added startup and manual checks against the latest published GitHub Release, with direct access from Help.

### Fixed
- **Session counters:** Loading a new torrent now stops the previous session and resets uploaded, downloaded, completion, and announce counters to zero.
- **Version source:** Replaced the mutable `version.txt` branch lookup with GitHub's latest published release endpoint.

## [1.1.0] - 2026-09-13

- Expose the stable client key and peer ID used throughout a tracker session.
- Display torrent info-hash and discovered local IPv4/IPv6 addresses in the desktop interface.
- Add opt-in persistent debug logs and Help shortcuts to the GitHub repository and issue form.

### Added
- **Cross-platform desktop:** Added an Avalonia 12 application targeting Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon.
- **Portable engine:** Added `RatioForge.Core` for torrent metadata, client identities, tracker URL generation, and HTTP(S) announces.
- **Portable smoke checks:** Added a display-independent `--smoke-test` entry point to every published executable.
- **Release artifacts:** Added self-contained archives, raw executables, Lite executables, and SHA256 files for four runtime identifiers.
- **IPv6:** Added dual-stack HTTP and HTTPS tracker connections, literal `[IPv6]` tracker URLs, local IPv4/IPv6 address selection, and IPv6-safe URL encoding.

### Changed
- **Tests:** Retargeted the automated suite to portable `net10.0` and added coverage for the public core API.
- **CI:** Tests now run on Windows, Ubuntu, and macOS; packaging runs on native runners for each architecture.
- **Networking:** Replaced the obsolete version-checking `WebRequest` implementation with `HttpClient`.
- **Default identity:** New sessions now explicitly select the latest stable qBittorrent 5.2.3 profile in both desktop interfaces.
- **Settings:** Restored a cross-platform settings window with persisted session defaults, address selection, proxy configuration, rate randomization, and activity logging.

## [1.0.14] - 2026-09-11

### Changed
- **Runtime**: Migrated the application and test projects from .NET 8 to `net10.0-windows` on .NET 10 LTS.
- **Language**: Updated the compiler language version from C# 12 to C# 14.
- **SDK**: Added `global.json` with .NET 10 feature-band roll-forward for reproducible local and CI builds.
- **Dependencies**: Removed the redundant `System.Text.Encoding.CodePages` package now supplied by .NET 10.
- **CI**: Updated build and tagged-release workflows to install the .NET 10 SDK.
- **Docs**: Updated runtime requirements, build prerequisites, release checks, and project metadata for .NET 10.

### Fixed
- **WinForms**: Declared designer serialization behavior for custom panel properties required by current WinForms analyzers.
- **Network**: Made incoming BitTorrent handshake reads exact instead of assuming a single stream read fills the buffer.

## [1.0.13] - 2026-08-11

### Added
- **Emulation**: Added source-verified profiles for qBittorrent 5.2.3, Transmission 4.1.3, KTorrent 26.04.3, and BiglyBT 4.1.0.0.
- **Tests**: Added peer ID length, prefix, and User-Agent coverage for every new profile.

### Changed
- **UI**: Made the newest verified client versions the default choices in their respective selectors.
- **Audit**: Confirmed Deluge 2.2.0 remains the latest stable Deluge release; identified BitComet 2.21 but did not add an unverified tracker signature.

## [1.0.12] - 2026-08-11

### Added
- **Release**: Added a framework-dependent `win-x64-lite.exe` for systems with the .NET 8 Desktop Runtime installed.
- **CI**: Added version, startup, and maximum-size checks for both Windows executable variants.

### Changed
- **Release**: Enabled .NET single-file compression for the self-contained executable, reducing its measured local size from 161.7 MB to 71.6 MB.
- **Checksums**: Included the Lite executable in generated SHA256 checksums and post-upload release verification.

## [1.0.11] - 2026-08-11

### Added
- **Dependencies**: Added weekly Dependabot checks for NuGet packages and GitHub Actions.
- **Security**: Added a shared NuGet vulnerability audit that blocks build and release workflows on known direct or transitive vulnerabilities.
- **Tests**: Added malformed torrent fixtures for missing trackers, damaged piece hashes, truncated strings, and unterminated integers.
- **Tests**: Added tracker URL edge cases for case-insensitive scrape rewriting and malformed hexadecimal hashes.
- **Docs**: Added an architecture decision that archives the legacy PHP website and identifies the canonical project surfaces.

### Changed
- **Dependencies**: Updated all direct NuGet packages to the latest versions available from NuGet.org on 2026-08-11.
- **CI**: Added explicit artifact retention periods to build and release workflows.
- **Docs**: Marked every Short Term roadmap objective as Done with links to its implementation evidence.
- **Release**: Removed the archived website version file from the active release checklist.

### Fixed
- **Parser**: Reject truncated BEncode strings, integers, lists, and dictionaries instead of accepting partial data or reading indefinitely.
- **Parser**: Encode and report byte-string lengths from their byte count using invariant numeric formatting.

## [1.0.10] - 2026-08-11

### Added
- **Emulation**: Added Deluge 1.3.15 and 2.2.0 client IDs.
- **Emulation**: Added qBittorrent 4.2.3, 4.4.5, 4.5.5, 4.6.3, and 5.1.3 client IDs while retaining 5.1.2.
- **Tests**: Added peer ID length, prefix, and User-Agent coverage for the new client profiles.

### Fixed
- **Emulation**: Fixed Deluge announce URLs so tracker events are inserted exactly once.

## [1.0.9] - 2026-07-10

### Added
- **Tests**: Added single-file, multi-file, and malformed torrent fixtures with parser coverage.
- **Tests**: Added tracker announce, scrape, counter normalization, and hash encoding coverage.
- **CI**: Added a controlled application startup smoke check for published Windows builds.

### Changed
- **Architecture**: Extracted tracker announce, scrape, and hash URL generation from Windows Forms into a dedicated builder.
- **Release**: Windows artifacts are now self-contained single-file executables that do not require a separate .NET runtime.
- **Release**: Post-upload verification now downloads release assets and validates their SHA256 checksums.
- **Website**: Refreshed project, download, news, and history content and removed obsolete analytics, social scripts, and downloads.
- **Docs**: Updated the release checklist and roadmap to reflect completed work and remaining architecture steps.

### Fixed
- **Parser**: Register the Windows-1252 encoding provider in the BEncode parser instead of relying on UI startup.
- **Parser**: Preserve declared torrent file lengths without accessing nonexistent payload files on disk.
- **Parser**: Reset accumulated file and size state when reopening a torrent.

## [1.0.8] - 2026-07-09

### Added
- **CI**: Build workflow now publishes Windows artifacts with smoke checks and SHA256 checksums.
- **Release**: Release workflow now uploads the zip archive, raw GUI executable, and checksum file to GitHub Releases.
- **Release**: Release workflow now verifies uploaded GitHub release assets after publication.
- **Docs**: Added a .NET-focused release checklist covering versioning, verification, tagging, and rollback.
- **Docs**: Added a practical roadmap with short-, mid-, and long-term work.

### Changed
- **Docs**: Added a README badge for the release workflow status.

## [1.0.7] - 2026-05-22

### Fixed
- **Version Check**: Compare remote and local versions as semantic versions instead of ordinal strings.
- **Tests**: Removed the live network dependency from version checker tests.
- **Maintenance**: Replaced remaining StringBuilder TODOs in version logging and URL-safe random string generation.

### Changed
- **CI**: Updated GitHub Actions to Node 24-compatible official action versions and pinned the Windows runner to `windows-2025`.
- **Dependencies**: Updated NuGet packages to the latest versions available from the configured NuGet source.
- **Docs**: Aligned README and website version references with version 1.0.7.
- **Links**: Replaced obsolete donation/contact placeholders with the project support page.

## [1.0.6] - 2026-05-22

### Fixed
- **Emulation**: Refresh peer ID data when the selected torrent client or client version changes.
- **Emulation**: Fixed the uTorrent 3.2.0 peer ID profile so it resolves to a valid 20-byte peer ID.
- **Build**: Cleaned the Release build so it completes without compiler warnings.

### Changed
- **Release**: Centralized project version metadata and aligned `version.txt`, assembly metadata, runtime display, tests, and tagged release artifacts.

## [1.0.5] - 2026-03-19

### Fixed
- **Network**: Fixed Cloudflare HTTPS tracker connection hanging caused by missing `Connection: close` and Keep-Alive loops.
- **Network**: Fixed private tracker scrape URL generation when the passkey follows `/announce/`.
- **Network**: Repaired `sendEventToTracker` return logic so it correctly registers successful updates and initiates periodic background timer ticks.
- **UI**: Resolved silent `StackOverflowException` crash due to asynchronous `updateCounters` infinite fallback loops on exception.
- **UI**: Fixed bug in `SetPrecision` crashing the app when parsing highly precise fractional ratios with native culture decimal separators (e.g. `2.9E-5`).

## [1.0.4] - 2026-01-18

### Fixed
- **UI**: Fixed an issue where newly added clients (qBittorrent 5.1.2, uTorrent 3.6.0) were not visible in the client selection list (Fixes #2, Fixes #3).

## [1.0.3] - 2026-01-18

### Fixed
- **Critical Runtime Error**: Fixed `System.NotSupportedException: No data is available for encoding 1252` by registering `CodePagesEncodingProvider`. This is required for .NET 8 to support legacy encodings widely used in torrent files.

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

[1.1.1]: https://github.com/tsautier/RatioForge/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/tsautier/RatioForge/releases/tag/v1.1.0
[1.0.14]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.14
[1.0.13]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.13
[1.0.12]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.12
[1.0.11]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.11
[1.0.10]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.10
[1.0.9]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.9
[1.0.8]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.8
[1.0.7]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.7
[1.0.6]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.6
[1.0.5]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.5
[1.0.4]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.4
[1.0.3]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.3
[1.0.2]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.2
[1.0.0]: https://github.com/tsautier/RatioForge/releases/tag/v1.0.0
[0.43]: https://github.com/NikolayIT/RatioMaster.NET/releases/tag/v0.43
