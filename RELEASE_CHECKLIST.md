# Release Checklist

Use this checklist before creating and pushing a release tag.

## Versioning

- [ ] Update `version.txt`, `Source/Directory.Build.props`, version tests, `CHANGELOG.md`, `RELEASE_NOTES.md`, and README highlights.
- [ ] Keep the archived `Website/` unchanged; see `docs/decisions/0001-archive-legacy-website.md`.

## Verification

- [ ] Run `dotnet restore Source/RatioForge.sln`.
- [ ] Run `build/Test-NuGetSecurity.ps1`.
- [ ] Run `dotnet build Source/RatioForge.sln --configuration Release --no-restore`.
- [ ] Run `dotnet test Source/RatioForge.sln --configuration Release --no-build`.
- [ ] Run `build/Publish-Desktop.ps1 -Runtime win-x64` locally.
- [ ] Confirm both Windows executables pass `--smoke-test` and the GUI starts without application-log errors.
- [ ] Confirm the self-contained executable is at most 100 MiB and the Lite executable is at most 35 MiB.
- [ ] Let GitHub Actions build and smoke-test Linux x64, macOS Intel, and macOS Apple Silicon on native runners.

## Assets

- [ ] Confirm every runtime has an archive, raw self-contained executable, raw Lite executable, and `.sha256` file.
- [ ] Use `.zip` for `win-x64`, `.tar.gz` for Linux, and a `.tar.gz` containing `RatioForge.app` for each macOS architecture.
- [ ] Confirm the release workflow re-downloads all 16 assets and verifies every checksum.
- [ ] Do not add packaging tools for languages that are not used by the application.

## Commit And Tag

- [ ] Verify Git identity is `tsautier <tsautier@users.noreply.github.com>`.
- [ ] Commit release changes with that unique author identity.
- [ ] Create an annotated tag: `git tag -a v<version> -m "RatioForge <version>"`.
- [ ] Push `master`, then push `v<version>`.
- [ ] Confirm both Build and Release workflows pass and the GitHub release contains all expected assets.

## Rollback

- [ ] Delete a failed GitHub release before deleting its tag.
- [ ] Delete the remote tag with `git push origin :refs/tags/v<version>` and the local tag with `git tag -d v<version>`.
- [ ] Revert or fix the release commit on `master`, then repeat every verification step before tagging again.
