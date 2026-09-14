# RatioForge 1.1.2

RatioForge 1.1.2 corrects the initial session state and improves feedback for randomization and update checks.

- Uploaded and downloaded values now reliably start at `0 B`, with no announce countdown before a session starts
- The session timer starts only when the user starts a session and stops defensively while idle
- Randomized upload and download rates are always whole KiB/s values
- Randomization and session rate inputs display integer values
- Manual update checks now show an explicit result dialog for current, available, and failed checks
- Update results remain recorded in the activity and optional persistent debug logs
- Native self-contained and Lite builds for Windows x64, Linux x64, macOS Intel, and macOS Apple Silicon
- SHA256 verification files for every release package

The Lite executable requires the .NET 10 Runtime for the matching platform. The standard executable and archive are self-contained.
