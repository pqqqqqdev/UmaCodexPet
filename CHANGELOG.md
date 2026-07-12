# Changelog

All notable user-facing changes are recorded here.

## 0.3.0 - 2026-07-12

- Added an <kbd>F6</kbd> **Animations/Face** page with separate per-character
  motion and static Mini face choices for all nine canonical pet states.
- Added searchable, human-readable compatible motion lists whose friendly
  labels and search clearly mark clips as character-specific **Character**
  motions or shared **General** motions.
- Added blank-search recommendations that pin all compatible dance clips for
  idle, `near05` for cursor hover/jump, and matching character-specific
  specials alongside each state's normal choices.
- Selecting a motion now previews it immediately or automatically loads the
  matching Mini with the clothes currently chosen in F6. The motion list stays
  open so several clips can be compared back-to-back.
- Kept **Auto** as the default for every state. For all nine F6 states, a valid
  F6 choice takes precedence; otherwise the existing exact CSV, wildcard CSV,
  and automatic sources remain available in that order.
- Made the F6 picker resizable from its bottom-right corner and added a
  scrollable all-state page for compact window sizes.
- Made the picker modal and isolated its mouse-wheel, click, and drag input
  from UmaViewer's camera and controls while it is open.
- Added static per-state Mini face controls for left/right eyes, mouth, and
  left/right eyebrows. If a matching Mini is absent, the face editor now
  automatically queues the selected character and current F6 clothes before
  enabling its live controls.
- Added **Load Mini for preview**, **Retry preview**, and **Reload selected
  clothes** controls for starting or recovering motion and face previews.
- Added explicit motion and face sources and configured face slots to the
  per-character audit manifest.
- Preserved the Windows Codex host's fixed state timing, frame counts, and
  `192 × 208` cell limit; these controls change source poses, not host FPS or
  final sprite resolution.

## 0.2.0 - 2026-07-12

Initial public alpha.

- Added a searchable <kbd>F6</kbd> character picker inside UmaViewer with
  **Select All**, **Select Visible**, **Clear**, **Save**, and
  **Save & Export** actions.
- Fresh installs now start with no selected characters. The exact legacy
  12-character default is migrated to an empty selection, while custom rosters
  are preserved.
- Added searchable, per-character Mini clothes selection from outfits that are
  actually available in the local UmaViewer asset index, with **Auto** as the
  default.
- **Save** accepts an empty selection; **Save & Export** requires at least one
  character.
- Added stable character and mini-animation catalogs generated with <kbd>F7</kbd>.
- Added per-character and wildcard motion selection through an optional CSV file.
- Filtered the catalog to renderable Mini body clips and preserved signed
  64-bit motion keys as strings for browser-safe selection.
- Kept automatic motion resolution as the default; animation overrides remain
  an optional CSV-based advanced feature.
- Encoded the exact Windows Work-pet `8 × 9` layout: six idle frames, six
  working frames, the canonical slots for the other seven states, and all 15
  unassigned cells fully transparent.
- Added explicit documentation for the host-controlled frame rate and 192 by 208
  cell-resolution limits.
- Documented `atlas.png` as the canonical encoded sprite sheet and added a
  pre-release visual inspection and target-app smoke-test checklist.
- Added allowlisted release packaging with config templates and SHA-256 checksums.
