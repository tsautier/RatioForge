# RatioForge 1.1.4

RatioForge 1.1.4 consolidates tracker-session lifecycle behavior and restores useful safeguards from earlier RatioForge versions.

- Live completion, ratio, and elapsed-time indicators follow the simulated transfer counters
- Automatic stopping supports elapsed seconds, uploaded MiB, downloaded MiB, and target ratio
- Tracker rejection stops a session by default and can be disabled in Settings
- Session-defining controls stay locked while a session is active, keeping tracker identity stable
- A `completed` announce is sent when download progress reaches 100%
- A final `stopped` announce is attempted on stop, reset, torrent replacement, automatic stop, and application exit
- `Defaults` restores every setting to its documented initial value
- Portable automated tests cover automatic-stop thresholds, persisted settings, and new UI defaults
- Native self-contained and Lite builds for Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon
- SHA256 verification files for every release package

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
