# RatioForge 1.2.1

RatioForge 1.2.1 stabilizes the 1.2 tracker workflow and incorporates applicable compatibility fixes identified after reviewing current RatioMaster.NET changes.

- Filter and export redacted announce history as CSV or JSON
- Select an `announce-list` tracker manually and retry it immediately
- Pause upload accounting while the tracker explicitly reports zero leechers
- Request zero peers on stopped HTTP(S) and UDP announces
- Correct arbitrary Peer ID byte generation, Transmission check digits, and Transmission keys
- Rotate generated uTorrent and BitTorrent tracker keys every ten minutes
- Load modern emulations from an embedded and user-overridable `clients.json`
- Add current qBittorrent, uTorrent, BitTorrent, Transmission, Deluge, Vuze, and rTorrent profiles
- Redact local addresses and proxy usernames from support exports
- Add structured GitHub issue forms and a private security-reporting policy
- Native self-contained and Lite builds for Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon
- SHA256 verification files for every release package

HTTP and SOCKS proxies apply to HTTP(S) trackers. UDP trackers use direct UDP because those proxy protocols cannot relay BEP 15 datagrams through the current .NET transport.

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
