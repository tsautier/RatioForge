# RatioForge 1.2.2

RatioForge 1.2.2 strengthens compatibility testing, profile portability, announce diagnostics, and release-size observability.

- Compare refactored HTTP(S) announces with deterministic historical output for representative clients and lifecycle events
- Import and export versioned session profiles and data-driven client profiles from Settings
- Keep proxy passwords out of portable session-profile exports
- Inspect each announce attempt with an anonymized request, final destination, and protocol-specific diagnostic
- Copy the selected anonymized announce request in one click
- Cover CDN-style HTTPS passkeys, IPv6 UDP, complex tracker tiers, nested paths, and large multi-file torrents with anonymized fixtures
- Preserve full relative paths for files inside multi-file torrents
- Enforce a stricter 31 MiB limit for Lite single-file builds across all runtimes while retaining supported compression for self-contained bundles
- Publish per-runtime JSON metrics for startup time, peak memory, executable sizes, and Lite reduction
- Verify all 20 release assets and include metrics in each SHA256 manifest

HTTP and SOCKS proxies apply to HTTP(S) trackers. UDP trackers use direct UDP because those proxy protocols cannot relay BEP 15 datagrams through the current .NET transport.

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
