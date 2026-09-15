# RatioForge 1.2.0

RatioForge 1.2.0 completes the six medium-term networking and workflow improvements planned after 1.1.5.

- Structured announce history with redacted tracker, event, protocol, status, latency, interval, and result
- Readable pre-session diagnostics for DNS, IPv4, IPv6, TCP, TLS, proxy endpoints, and UDP tracker handshakes
- Ordered `announce-list` tiers with automatic failover and visible active tracker selection
- BEP 15 UDP tracker announces over IPv4 and IPv6, including legacy client Peer ID decoding
- Named session profiles for client, rates, interval, port, proxy, source address, and randomization settings
- Platform-aware update downloads with release notes, metadata size checks, and SHA256 verification before opening
- Privacy-filtered diagnostics now include active tracker and structured announce history
- Automated coverage for torrent tiers, UDP packets and loopback exchanges, profiles, diagnostics, update selection, checksum rejection, and new Avalonia controls
- Native self-contained and Lite builds for Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon
- SHA256 verification files for every release package

HTTP and SOCKS proxies apply to HTTP(S) trackers. UDP trackers use direct UDP because those proxy protocols cannot relay BEP 15 datagrams through the current .NET transport.

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
