# RatioForge 1.1.1

RatioForge 1.1.1 fixes session counter carry-over and adds appearance and update controls to the cross-platform desktop application.

- Loading a new torrent stops the previous session and resets uploaded/downloaded counters, completion, and announce timing
- Appearance setting with System, Dark, and Light modes
- Automatic startup check against the latest published GitHub release
- Manual update check and latest-release shortcut in the Help menu
- Native self-contained and Lite builds for Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon
- SHA256 verification files for every release package

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
