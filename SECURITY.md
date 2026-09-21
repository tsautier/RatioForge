# Security Policy

## Supported versions

Security fixes are provided for the latest published RatioForge release. Upgrade before reporting a problem that only affects an older version.

## Private reporting

Do not publish tracker passkeys, authentication tokens, torrent names, info hashes, peer IDs, client keys, proxy credentials, or private tracker URLs in a public issue.

Report vulnerabilities through [GitHub private vulnerability reporting](https://github.com/tsautier/RatioForge/security/advisories/new). Include the affected version, operating system, reproduction steps, impact, and a minimally redacted diagnostic sample.

## Diagnostic data

RatioForge redacts common credentials from activity logs, persistent debug logs, copied diagnostics, tracker history, and exported history. Review any diagnostic material before sharing it: custom tracker URL formats may contain identifiers the application cannot recognize automatically.

RatioForge does not transfer torrent payload data. It contacts configured trackers and GitHub Releases for update checks. Proxy passwords remain in memory for the current process and are not written to settings or session profiles.

## Response process

Reports are acknowledged as soon as practical. A confirmed issue is reproduced privately, fixed with regression coverage, and released through the normal checksum-verified workflow. Public details are deferred until an update is available when disclosure would expose users.
