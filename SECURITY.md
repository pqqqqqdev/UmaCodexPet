# Security policy

UmaPetForge is a local desktop plugin. It runs inside UmaViewer, reads asset
metadata that UmaViewer has already indexed, and writes images to disk. Treat
the plugin with the same trust you give any other executable loaded into a
desktop application.

## Supported versions

Security fixes are made against the latest release and the current `main`
branch. Older releases may not receive backports.

| Version | Supported |
| --- | --- |
| Latest release | Yes |
| `main` | Best effort |
| Older releases | No |

## Reporting a vulnerability

Use GitHub's **Report a vulnerability** button to open a private security
advisory when private reporting is enabled. If that option is unavailable,
open a minimal issue asking the maintainers for a private contact method; do
not include vulnerability details in the issue.

Include:

- the affected UmaPetForge and UmaViewer versions;
- the Windows and BepInEx versions;
- clear reproduction steps;
- the security impact and realistic attack conditions; and
- a minimal proof of concept, if one is safe to share.

Do **not** attach game asset bundles, database files, extracted models,
textures, animations, access tokens, or other proprietary material. Redact
usernames and local installation paths from logs.

Maintainers aim to acknowledge reports within seven days, then validate the
report, coordinate a fix and release, and credit the reporter if requested.

## Scope

Reports about unsafe file handling, path traversal, unintended network access,
arbitrary code execution introduced by UmaPetForge, or insecure release
packaging are in scope.

Bugs in Uma Musume: Pretty Derby, UmaViewer, Unity, BepInEx, or the operating
system should be reported to their respective maintainers. A dependency issue
is in scope here when UmaPetForge makes it exploitable in a new way or can
reasonably mitigate it.

## Release safety

- Download releases only from this repository's Releases page.
- Verify the archive checksum when one is published.
- Install only into a dedicated UmaViewer folder, never into the game folder.
- Review changes to installer or packaging scripts before running them.
- Keep generated sprite sheets private unless you have the right to share the
  underlying material.
