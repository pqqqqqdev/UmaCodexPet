# UmaCodexPet

[![Build](https://github.com/pqqqqqdev/UmaCodexPet/actions/workflows/build.yml/badge.svg)](https://github.com/pqqqqqdev/UmaCodexPet/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/pqqqqqdev/UmaCodexPet?display_name=tag&sort=semver)](https://github.com/pqqqqqdev/UmaCodexPet/releases/latest)
[![License](https://img.shields.io/github/license/pqqqqqdev/UmaCodexPet)](LICENSE)

**Batch-export authentic, locally rendered Uma Musume chibi animations as
Codex desktop custom pets.**

[Download the latest release](https://github.com/pqqqqqdev/UmaCodexPet/releases/latest)
· [Quick start](#quick-start)
· [Documentation](#documentation)
· [Report a bug](https://github.com/pqqqqqdev/UmaCodexPet/issues/new?template=bug_report.yml)

UmaCodexPet is a small BepInEx plugin for UmaViewer. It uses the asset index
already loaded by UmaViewer, renders game-authored Mini models and motions on
your machine, and assembles transparent PNG atlases in the format expected by
Codex desktop custom pets.

No AI-generated character art is involved. No Cygames models, textures,
animations, databases, game files, UmaViewer binaries, or BepInEx binaries are
included in this repository or its plugin archive.

> [!IMPORTANT]
> UmaCodexPet is an unofficial interoperability tool for local use. It is not
> affiliated with or endorsed by Cygames, the UmaViewer maintainers, Unity, or
> OpenAI. You must supply a legitimate game installation and comply with all
> applicable terms. Do not redistribute generated sheets unless you have the
> right to redistribute the underlying material.

> [!WARNING]
> **Current Windows Codex desktop limitation:** custom pet atlases are fixed at
> `1536 × 1872`, sliced as an `8 × 9` grid of `192 × 208` cells. The Windows
> app currently ignores custom `fps` values and custom frame maps in
> `pet.json`; it uses its built-in state timing and frame slots instead.
> UmaCodexPet therefore cannot raise the displayed animation FPS, add new pet
> states, or change the canonical frame counts on Windows. Motion selection
> changes which UmaViewer clip is rendered into each existing state. Rendering
> at 4× before downsampling improves edge quality, but it cannot increase the
> final per-cell resolution.

## Highlights

- **Choose visually.** Search characters and locally available Mini outfits in
  the <kbd>F6</kbd> picker, then preview per-character motions and static Mini
  faces for all nine canonical pet states.
- **Keep everything local.** UmaCodexPet reads UmaViewer's in-memory index and
  the user's own installation; it does not download game data, extract reusable
  3D files, upload anything, or modify the Uma Musume installation.
- **Export deterministically.** Automatic motion selection, explicit picker
  choices, and optional CSV overrides resolve in a documented, auditable order.
- **Validate every sheet.** The exporter calibrates one camera across the
  selected motion envelope, rejects clipped frames, keeps unused cells
  transparent, and records its decisions in manifests.

## Requirements

- Windows 10 or 11 x64
- Uma Musume: Pretty Derby on Steam with **Download All** completed
- [UmaViewer](https://github.com/katboi01/UmaViewer), configured and able to
  display a Mini character
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for Unity Mono x64

The initial release targets the Global Steam workflow. The exporter operates
on UmaViewer's loaded index, so other regions may work, but should be treated
as unverified until documented otherwise.

## Quick start

1. In Uma Musume, finish **Settings → Download All**.
2. Install and run UmaViewer. For Global Steam, set **WorkMode** to `Default`
   and **Region** to `Global`, then confirm that a character loads under
   **Characters → Mini**.
3. Install the BepInEx 5 **Windows x64 Unity Mono** archive into the folder
   containing `UmaViewer.exe`. Launch UmaViewer once so BepInEx creates its
   folders, then close it.
4. [Download the latest UmaCodexPet release](https://github.com/pqqqqqdev/UmaCodexPet/releases/latest)
   and extract its ZIP into the same UmaViewer folder. Confirm this file exists:

   ```text
   <UmaViewer>\BepInEx\plugins\UmaCodexPet\UmaCodexPet.dll
   ```

5. Start UmaViewer and wait for its character lists and preview to finish
   loading.
6. Keep the UmaViewer window focused and press <kbd>F6</kbd>.
7. Select the characters you want. Use **Clothes** and **Animations/Face** for
   any explicit choices; leave a choice on **Auto** for normal resolution.
8. Choose **Set F8 Batch** for a later <kbd>F8</kbd> export, or **Set F8 Batch &
   Export** to store the batch, close the picker, and start immediately.
9. Wait for `EXPORT_COMPLETE.txt`, inspect the generated atlas, and smoke-test
   the pet through the target app's supported custom-pet workflow.

Every <kbd>F6</kbd> session opens with no characters selected and every outfit,
motion, and face on **Auto**. The most recently set batch remains available to
closed-picker <kbd>F8</kbd> without repopulating a new picker draft.

By default, results appear under:

```text
<UmaViewer>\UmaCodexPet_Output\<timestamp>\
```

## Controls

| Key | Action |
| --- | --- |
| <kbd>F6</kbd> | Open the clean character, clothes, motion, and face picker |
| <kbd>F7</kbd> | Refresh catalogs and create the optional advanced override template |
| <kbd>F8</kbd> | Export the most recently set batch after reloading config and CSV overrides |

The export key starts one batch only after UmaViewer's database, renderer,
camera, character list, and shaders are ready. Pressing <kbd>F8</kbd> again while
a run is active is intentionally ignored.

## Documentation

- [Using the picker](https://github.com/pqqqqqdev/UmaCodexPet/blob/main/docs/using-the-picker.md) — character, outfit, motion,
  face, catalog, and CSV selection details
- [Output format and internals](https://github.com/pqqqqqdev/UmaCodexPet/blob/main/docs/output-and-internals.md) — canonical atlas
  layout, manifests, configuration, and render pipeline
- [Troubleshooting](https://github.com/pqqqqqdev/UmaCodexPet/blob/main/docs/troubleshooting.md) — installation, preview, motion,
  face, FPS, resolution, framing, and transparency problems
- [Advanced validation tools](https://github.com/pqqqqqdev/UmaCodexPet/blob/main/docs/advanced-tools.md) — deterministic atlas
  normalization and diagnostic preview generation
- [Contributing](https://github.com/pqqqqqdev/UmaCodexPet/blob/main/.github/CONTRIBUTING.md) — development, testing, release, and
  screenshot-publication rules
- [Changelog](CHANGELOG.md) — released and pending user-facing changes

## Output and compatibility

Each selected character receives a canonical `1536 × 1872` RGBA `atlas.png`, a
`pet.json`, resolved selection data, and optional individual frames. Batch and
per-character manifests keep the outfit, motion, face, fitting, and warning
decisions reproducible without sharing game assets.

UmaCodexPet creates output files only. It does not publish or activate pets.
The target Windows app controls state timing and currently ignores custom frame
maps and FPS metadata. See [Output format and internals](https://github.com/pqqqqqdev/UmaCodexPet/blob/main/docs/output-and-internals.md)
for the exact cell allocation and generated directory structure.

## Building from source

Install .NET SDK 8 or newer and point the build script at a clean UmaViewer
folder. Its managed assemblies are compile-time references only and are never
copied into the package.

```powershell
.\scripts\build.ps1 -UmaViewerDir "C:\Tools\UmaViewer" -Install
```

Omit `-Install` to build and stage `artifacts\package` without changing the
test installation. See [Contributing](https://github.com/pqqqqqdev/UmaCodexPet/blob/main/.github/CONTRIBUTING.md) for the full
development and release process.

## Legal and licensing

UmaCodexPet's original source and documentation are available under the
[MIT License](LICENSE). That license applies only to this repository's work; it
does not cover UmaViewer, BepInEx, the game, or exported images.

Read [THIRD_PARTY.md](THIRD_PARTY.md) before distributing a build or output.
Security issues should follow the [security policy](https://github.com/pqqqqqdev/UmaCodexPet/blob/main/.github/SECURITY.md).

## Acknowledgements

- [UmaViewer](https://github.com/katboi01/UmaViewer) for making local model and
  animation viewing possible.
- [BepInEx](https://github.com/BepInEx/BepInEx) for the Unity Mono plugin
  runtime.

Acknowledgement does not imply affiliation or endorsement.
