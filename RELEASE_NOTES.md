# RatioForge 1.2.4

RatioForge 1.2.4 is a stability and maintenance release for the desktop application.

- Report unexpected failures in desktop actions and the session timer in Activity and the status bar instead of closing the application
- Share a single shutdown between simultaneous stop requests (Stop, automatic stop, tracker rejection, window close) and send one `stopped` announce
- Always release session controls and allow the window to close, even if the final `stopped` announce fails
- Show file-picker and unexpected import/export errors in Settings as validation messages
- Remove the unmaintained legacy WinForms project; the shared parser and announce code now lives in `RatioForge.Core`

HTTP and SOCKS proxies apply to HTTP(S) trackers. UDP trackers use direct UDP because those proxy protocols cannot relay BEP 15 datagrams through the current .NET transport.

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
