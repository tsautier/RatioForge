# Client profile compatibility audit - 2026-09-21

Reference reviewed: RatioMaster.NET 1.1.0 changelog and current MIT-licensed source at <https://github.com/NikolayIT/RatioMaster.NET>.

RatioForge is a fork, so changes made upstream after the fork are not inherited automatically. Each item was checked against the current RatioForge engine before being adapted.

## Applicable and addressed in 1.2.1

- A stopped announce requests zero peers for both HTTP(S) and UDP.
- Arbitrary peer ID bytes are generated in the inclusive range 1-255, avoiding NUL and allowing `0xff`.
- Transmission 2.94 and 3.00 use the base-36 check digit used by Transmission and its variable-length hexadecimal tracker key.
- Generated uTorrent and BitTorrent tracker keys rotate every ten minutes while the session runs.
- Upload accounting pauses while a tracker explicitly reports zero leechers and resumes when leechers return. Unknown swarm state does not pause accounting.
- Current emulations are data-driven through an embedded `clients.json`; an optional configuration file can add or replace entries at startup.
- Modern profiles include qBittorrent 5.2.3, 5.1.4 and 4.6.7; uTorrent 3.6.0, 3.5.5 and 3.5.4; BitTorrent 7.10.3; Transmission 2.94 and 3.00; Deluge 2.1.1; Vuze 5.7.5.0; and rTorrent 0.9.6.

## Already covered before 1.2.1

- HTTPS, IPv4, IPv6, redirects and decompression use the .NET transport.
- Tracker intervals are accepted up to 24 hours.
- Deluge events are emitted once and malformed tracker input is covered by offline tests.
- Activity and announce history are bounded, preventing an unbounded UI collection.

## Not copied directly

- RatioForge keeps legacy profiles for saved-settings compatibility instead of silently replacing them. Modern profiles are listed first.
- SOCKS4 and SOCKS4a are not implemented; the supported proxy modes remain system, HTTP and SOCKS5.
- Incoming peer-handshake simulation and simultaneous multi-torrent sessions are separate architectural features, not regression fixes.

Profile values derived from upstream remain subject to verification against each client's published source or captured announces before future changes are released.
