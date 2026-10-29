# RatioForge 1.2.5

RatioForge 1.2.5 improves tracker reliability and hardens the session engine.

- Fail over to the next `announce-list` candidate when a tracker times out, and schedule a retry instead of reporting the announce as cancelled
- Reject torrents whose `announce` or `name` value is not a string with a readable error
- Rotate `debug.log` to `debug.log.1` once it reaches 5 MiB
- Write Windows `.sha256` manifests with LF line endings so `sha256sum -c` works on every platform
- Move session accounting and tracker failover into `RatioForge.Core`, covered by end-to-end tests against local HTTP trackers
- Update Avalonia desktop packages to 12.1.4

HTTP and SOCKS proxies apply to HTTP(S) trackers. UDP trackers use direct UDP because those proxy protocols cannot relay BEP 15 datagrams through the current .NET transport.

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
