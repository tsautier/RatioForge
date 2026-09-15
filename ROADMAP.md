# Roadmap

This roadmap is intentionally practical: it separates near-term maintenance from larger product and architecture work.

## Short Term - Done

Completed through 2026-09-15.

- [x] **Done - Release automation:** Build and tagged-release workflows run tests, create self-contained Windows artifacts, launch the executable, generate SHA256 checksums, upload artifacts, and re-download release assets for checksum verification. See [build.yml](.github/workflows/build.yml) and [release.yml](.github/workflows/release.yml).
- [x] **Done - Dependency maintenance:** Direct NuGet dependencies are current, Dependabot checks NuGet and GitHub Actions weekly, and CI rejects known direct or transitive NuGet vulnerabilities. See [dependabot.yml](.github/dependabot.yml) and [Test-NuGetSecurity.ps1](build/Test-NuGetSecurity.ps1).
- [x] **Done - Parser and URL edge cases:** Automated fixtures cover single-file, multi-file, missing metadata, damaged piece hashes, truncated strings, and unterminated integers; tracker tests cover announce events, scrape rewriting, query preservation, and malformed hashes. See [RatioForge.Tests](Source/RatioForge.Tests).
- [x] **Done - Reproducible releases:** Versioning, local verification, packaging, tagging, GitHub asset checks, and rollback are documented in [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).
- [x] **Done - Legacy website decision:** The PHP website is retained as an undeployed historical archive; README, changelog, `version.txt`, and GitHub Releases are canonical. See [ADR 0001](docs/decisions/0001-archive-legacy-website.md).
- [x] **Done early - Cross-platform desktop:** The primary Avalonia application and portable .NET 10 core run on Windows, Linux, and macOS, with CI packaging for `win-x64`, `linux-x64`, `osx-x64`, and `osx-arm64`.
- [x] **Done - Safe diagnostics:** Activity and persistent logs redact tracker credentials; users can open the log, copy a filtered diagnostic report, or clear activity from the desktop interface.
- [x] **Done - Session controls:** Manual updates immediately announce current transfer statistics, while reset clears counters and regenerates identity without reloading the torrent.
- [x] **Done - Input feedback:** Randomization ranges are restricted to whole KiB/s values and invalid minimum/maximum combinations are shown before saving.
- [x] **Done - UI regression tests:** Portable Avalonia headless coverage verifies empty startup counters, session-action defaults, appearance modes, validation feedback, and update dialogs.
- [x] **Done - Session lifecycle parity:** Active sessions keep a stable identity, report live completion/ratio/time, send completion and stop lifecycle events, and support automatic stop thresholds.

## Medium Term - 1.2.x

6. **Structured announce history:** Add a table containing time, event, tracker, protocol, latency, HTTP status, interval, and result while keeping sensitive values redacted.
7. **Network diagnostics:** Test DNS, IPv4, IPv6, TLS, and proxy connectivity before a session starts, and present readable results instead of raw exceptions.
8. **`announce-list` management:** Support tracker tiers from torrent metadata, fail over to a secondary tracker, and display the tracker currently in use.
9. **UDP tracker support:** Extend the engine to `udp://` trackers with protocol-specific network tests. This is intentionally planned as a minor release feature.
10. **Session profiles:** Save multiple named configurations containing emulated client, rates, port, proxy, local address, and randomization settings.
11. **Improved updates:** Offer the correct download for the current operating system and architecture, display release notes, and verify SHA256 before opening it.

Additional 1.2.x maintenance:

- Add a dedicated .NET CLI project if command-line automation is needed.
- Add structured logging for tracker communication, version checks, and proxy failures.
- Improve error handling around network, proxy, and malformed torrent files.
- Completed early: sample torrent fixtures now cover parser and tracker behavior; continue extending them when regressions are found.
- Active: CI enforces startup checks and size budgets for the compressed self-contained and Lite single-file releases.
- Add a signed release path if code-signing certificates become available.
- Package a signed and notarized macOS `.app` bundle when Apple signing credentials become available.

## Long Term

- Completed early: the tracker engine and torrent parser now live in the reusable `RatioForge.Core` library.
- Completed early: the Avalonia UI refresh preserves the lightweight tracker-session workflow across desktop platforms.
- Add a documented plugin or profile system for torrent client emulation data.
- Build a dedicated .NET CLI executable if command-line workflows become part of the product.
- Extend automated compatibility testing beyond the current Windows, Ubuntu, macOS Intel, and macOS Apple Silicon runners.
- Define a security and disclosure policy for tracker, proxy, and release-distribution issues.
