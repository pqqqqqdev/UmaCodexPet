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

Point the build script at the directory containing `UmaViewer.exe`:

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

## Licensing

By submitting a contribution, you agree that it may be distributed under the
[MIT License](LICENSE). Only submit work you have the right to license. See
[THIRD_PARTY.md](THIRD_PARTY.md) for the project's dependency and asset
boundaries.
