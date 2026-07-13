# Contributing to UmaCodexPet

Thanks for helping make local, reproducible pet exports easier. Small bug
fixes, renderer compatibility improvements, motion-selection rules,
documentation, and validation tests are all welcome.

## Before opening an issue

1. Reproduce the problem with the latest UmaCodexPet and UmaViewer releases.
2. Check existing issues for the same symptom.
3. Collect the UmaCodexPet log and the smallest useful set of reproduction
   details.
4. Remove usernames and local paths before posting.

Never upload game files, database files, asset bundles, extracted models,
textures, animations, or generated sheets containing material you cannot
redistribute. A filename, asset identifier, and redacted log are usually
enough.

Use a private security advisory instead of an issue for vulnerabilities; see
[SECURITY.md](SECURITY.md).

## Development setup

The plugin targets the Mono Windows build of UmaViewer.

You will need:

- Windows 10 or 11 x64;
- .NET SDK 8 or newer;
- a clean UmaViewer installation that launches successfully;
- BepInEx 5 for Unity Mono x64; and
- legally obtained local game data for runtime testing.

From the repository root, point the build script at the directory containing
`UmaViewer.exe`:

```powershell
.\scripts\build.ps1 -UmaViewerDir "C:\Tools\UmaViewer"
```

The project resolves UmaViewer and Unity reference assemblies from
`<UmaViewer>\UmaViewer_Data\Managed`. These are build references only; do not
commit or redistribute them. The packaged plugin is staged under
`artifacts\package`.

To copy the resulting DLL into the test installation as part of the build, run:

```powershell
.\scripts\build.ps1 -UmaViewerDir "C:\Tools\UmaViewer" -Install
```

The installed path is:

```text
<UmaViewer>\BepInEx\plugins\UmaCodexPet\UmaCodexPet.dll
```

Launch UmaViewer and inspect `BepInEx\LogOutput.log` for plugin messages.

## Pull requests

- Keep each pull request focused on one change.
- Explain the user-visible behavior and the reason for the change.
- Include test steps and representative, non-proprietary logs.
- Update documentation when behavior or configuration changes.
- Preserve deterministic frame ordering and transparent output.
- Avoid hard-coding machine-specific paths or character-specific copyrighted
  data into the repository.
- Do not add telemetry, network calls, or automatic downloads without prior
  maintainer discussion and prominent documentation.

If a visual regression needs evidence, use an original test fixture or a
redacted diagnostic image that you are allowed to publish. Do not add game
screenshots or extracted assets to the repository.

## Motion rules

Motion-selection changes should be explainable and auditable. Prefer stable
asset-name rules and deterministic tie-breaking over opaque heuristics. Record
the chosen asset identifiers in diagnostic output so users can reproduce an
export without distributing the assets themselves.

## Pre-release testing

Do not publish a release based on a successful build alone. For every newly
selected character, outfit, motion, or face:

1. In F6, toggle one character directly and confirm its Mini appears. Choose an
   explicit outfit, then Auto, and confirm each reloads the preview. While a
   load is active, choose several characters or outfits quickly and confirm the
   final visible Mini matches the latest choice without closing the picker.
   Cancel and reopen to confirm the picker is clean. Repeat with **Set F8
   Batch**, reopen F6 to confirm it is still clean, then close it and verify F8
   exports the batch that was set.
2. Verify that opening a face editor with a different Mini loaded queues the
   selected character and current F6 clothes. Exercise **Retry preview** and
   **Reload selected clothes** at least once on the test machine.
3. With a blank motion search, confirm all compatible dances are recommended
   for idle, `near05` is recommended for hover/jump, and matching
   character-specific specials appear. Click several choices and confirm each
   previews while the list remains open; check that **Character** and
   **General** labels and searches return the expected scopes.
4. Resize the picker from its bottom-right grip. At its minimum and maximum
   sizes, scroll every subpage and confirm that clicking, dragging, or using the
   wheel never changes UmaViewer's FOV, camera height, distance, or scene view.
5. Set one unique motion and face for each of the nine states, export, and
   confirm the manifests contain those choices. Reopen F6 and confirm the new
   picker starts with every choice on Auto.
6. Confirm `EXPORT_COMPLETE.txt` exists and the batch and per-character
   manifests report success.
7. Inspect the encoded `atlas.png` itself: it must be exactly `1536 × 1872`,
   every assigned frame must fit its `192 × 208` cell, and all 15 unused cells
   documented in [Output format and internals](../docs/output-and-internals.md)
   must be fully transparent.
8. Review every canonical state for wrong clips, clipping, edge contamination,
   neighbor-cell fragments, and inconsistent scale. Diagnostic previews are
   useful here, but they do not replace inspection of `atlas.png`.
9. Smoke-test the generated pet in the target Windows app version through its
   supported custom-pet workflow. Expect idle cadence to remain host-controlled;
   a slow idle is not evidence that the exporter can override the app's FPS.
10. Before attaching release files, verify that the archive contains the
    plugin, documentation, and configuration templates only—never generated
    atlases or proprietary game data.

## Releases

Release only a commit that has passed CI and a local UmaViewer smoke test.
Before starting a release:

1. Move the relevant `CHANGELOG.md` entries out of **Unreleased** into an exact
   `## X.Y.Z - YYYY-MM-DD` section.
2. Make the versions in the project and plugin sources match that heading.
3. Create `release/vX.Y.Z` at the current default-branch commit.

The workflow rejects stale release branches, rebuilds the package, validates
and uses that exact changelog section as the curated GitHub release body,
creates the matching `vX.Y.Z` tag, and uploads both the ZIP and SHA-256 file. It
does not generate PR-title or contributor-list release notes.

## Publishing screenshots

The main README does not require screenshots. Add optional project screenshots
only after they are cleared for publication; do not commit a generated pet
atlas merely to make the repository look complete.

| Suggested filename | Capture | Redaction checklist |
| --- | --- | --- |
| `01-mini-loaded.png` | UmaViewer showing one correctly loaded Mini model | Crop other desktop content; remove local paths |
| `02-f6-animations-face.png` | Resized F6 Animations/Face page showing the scrollable nine-state list, Auto choices, and live-preview status | Crop other desktop content; do not expose local paths |
| `03-f6-motion-recommendations.png` | Blank idle or hover/jump motion list showing dance/near05 and Character/General labels | Avoid exposing raw local paths; show the list remaining open during comparison |
| `04-f6-face-preview.png` | Static Mini face-slot editor beside the automatically loaded selected Mini and F6 outfit | Crop other desktop content; confirm screenshot rights first |
| `05-f6-preview-retry.png` | Preview failure state with the Retry preview control | Use a harmless staged failure; remove paths and unrelated error details |
| `06-selection-catalogs.png` | The F7-generated catalog folder and a comments-only override template | Remove username and install path; show no proprietary bundles |
| `07-export-complete.png` | UmaCodexPet completion log beside `EXPORT_COMPLETE.txt` | Remove username, install path, and unrelated logs |
| `08-atlas-preview.png` | One atlas in a checkerboard transparency viewer | Confirm redistribution permission first |

Place approved files under `docs/screenshots/` and reference them from the
relevant root README or documentation section with relative Markdown links.

Do not commit game files, asset bundles, databases, extracted 3D models,
textures, or animations. A screenshot can still contain copyrighted material;
its small size or diagnostic purpose does not automatically make publication
permitted.

## Current roadmap

- tune and expand curated motion rules from real manifests;
- expand motion recommendations and investigate animated Mini face sequences;
- add a visual framing report for extreme motions and accessories;
- provide an in-app preview and validation report before final assembly;
- track Codex desktop support for custom frame maps and timing without claiming
  capabilities the host does not expose;
- add compatibility tests and verify release reproducibility in CI; and
- document additional UmaViewer regions only after they are verified.

The project deliberately does not plan to distribute game assets or silently
upload generated sheets.

## Licensing

By submitting a contribution, you agree that it may be distributed under the
[MIT License](../LICENSE). Only submit work you have the right to license. See
[THIRD_PARTY.md](../THIRD_PARTY.md) for the project's dependency and asset
boundaries.
