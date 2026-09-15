# RatioForge 1.1.5

RatioForge 1.1.5 fixes a tracker-accounting regression found by comparing matched legacy and current announce logs.

- Starting at 100% now sends `downloaded=0&left=0`, matching RatioForge 1.0.12 instead of reporting the full torrent as newly downloaded
- Starting at a partial percentage sends zero session-downloaded bytes and the correct remaining byte count
- Simulated download counters advance from the selected initial completion without exceeding the remaining data
- Verbose debug logs include runtime/network context, session settings, `uploaded/downloaded/left`, HTTP status and protocol, CDN/server, content metadata, latency, final redirected URL, swarm statistics, and IPv4/IPv6 peer counts without exposing peer addresses
- Detailed diagnostics remain visible in Activity by default, while persistent file logging remains independently opt-in
- Copied diagnostics mask the loaded torrent name as well as tracker credentials and session identifiers
- Regression tests cover complete and partial initial states, impossible remaining values, HTTP failure metadata, tracker statistics, and diagnostic filename privacy
- Native self-contained and Lite builds for Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon
- SHA256 verification files for every release package

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
