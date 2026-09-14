# RatioForge 1.1.3

RatioForge 1.1.3 restores direct tracker updates and completes the current short-term reliability and diagnostics work.

- `Manual update` sends the active session's current uploaded and downloaded statistics to the tracker immediately
- `Reset` stops an active tracker session, clears counters and completion, and regenerates the client identity without reloading the torrent
- Debug log shortcuts are available from the main activity panel and Settings
- `Copy diagnostics` creates a support report without torrent hashes, client keys, peer IDs, or unfiltered tracker credentials
- Activity and persistent logs redact tracker passkeys, URL credentials, API keys, tokens, client keys, peer IDs, info-hashes, and proxy secrets
- Activity can be cleared directly from the main window
- Invalid random upload/download bounds are shown immediately and prevent saving
- Portable Avalonia headless tests cover startup counters, session action availability, themes, range validation, and update dialogs
- Native self-contained and Lite builds for Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon
- SHA256 verification files for every release package

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
