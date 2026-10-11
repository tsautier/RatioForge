# RatioForge 1.2.3

RatioForge 1.2.3 is a focused stability release for Linux torrent loading.

- Prevent recursive tracker-selector refreshes when a torrent is loaded
- Prevent the same reentrancy when manually selecting an `announce-list` candidate
- Add an Avalonia regression test covering multi-tracker loading and selection
- Update Avalonia desktop packages to 12.1.3
- Update NUnit to 5.0.0

HTTP and SOCKS proxies apply to HTTP(S) trackers. UDP trackers use direct UDP because those proxy protocols cannot relay BEP 15 datagrams through the current .NET transport.

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
